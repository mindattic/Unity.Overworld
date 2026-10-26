using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The party in play, its locomotion and its following (U.OVW-US-A5, A7 and A8; RECONSTRUCTION 3.5, 3.7, 4.3 to 4.7,
    /// 5.1, 5.4 and 5.6). Each tick it does what the original's frame did: every player's held input is applied to the hero
    /// that player leads (getKeyboard's arrows and run key, getMouse's move-to-cursor, then KeysUp), crowding followers
    /// slide apart (checkFlocking), then every hero runs its AI state (aiStanding, aiWalking, aiRunning, aiKneeling: the
    /// velocity steps toward its speed and the walk or run cycle advances on the clock), a follower is steered after the
    /// leader or recalled beside it, or kneels when the team is off (checkfollow), the walls push its velocity back
    /// (checkWall, <see cref="WallCollision"/>), a hero below the ground rises into place (checkFall), and it moves by its
    /// velocity times the game speed. Installed on a <see cref="Game"/>, which then accepts <see cref="SetMoveInput"/>,
    /// <see cref="RecallHero"/>, <see cref="ToggleTeam"/>, <see cref="SelectHero"/> and <see cref="CycleLeader"/>;
    /// presentation reads <see cref="Heroes"/> and <see cref="Team"/>.
    /// </summary>
    /// <remarks>
    /// A follower is a hero no player in the game leads. Followers follow the hero the captain leads: the player in the
    /// game with the lowest id (with one player, the original's currHero). The team flag is the party's, as bTeam was.
    /// </remarks>
    public sealed class Party : ITickSystem
    {
        readonly List<Hero> heroes = new List<Hero>();
        readonly Dictionary<PlayerId, MoveInput> inputs = new Dictionary<PlayerId, MoveInput>();
        readonly Dictionary<PlayerId, int> leaders = new Dictionary<PlayerId, int>();

        readonly Random random;

        /// <param name="data">The heroes, their art and their movement.</param>
        /// <param name="walls">The walls that stop them (null: none).</param>
        /// <param name="random">Where a recalled hero lands within the spread (getRndPercent); null for an unseeded one.</param>
        public Party(HeroData data, WallData walls = null, Random random = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.Sprites == null || data.Locomotion == null || data.Fall == null || data.Follow == null || data.Flocking == null || data.Selection == null)
                throw new ArgumentException("The hero data has no sprites, locomotion, fall, follow, flocking or selection.", nameof(data));
            Walls = walls;
            this.random = random ?? new Random();
            Team = data.Follow.TeamAtStart;
            for (var i = 0; i < data.Heroes.Count; i++)
            {
                var hero = new Hero(i, data.Heroes[i]);
                hero.Sprite = data.Sprites.Stand(hero.Facing);
                heroes.Add(hero);
            }
        }

        public HeroData Data { get; }

        /// <summary>The walls the heroes cannot walk through (null: none).</summary>
        public WallData Walls { get; }
        public IReadOnlyList<Hero> Heroes => heroes;
        LocomotionTuning Tuning => Data.Locomotion;
        HeroSprites Sprites => Data.Sprites;

        /// <summary>Team mode (bTeam): the followers follow; off (solo), they kneel.</summary>
        public bool Team { get; private set; }

        /// <summary>Registers the party's commands and the per-tick rules on <paramref name="game"/>.</summary>
        public Party Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<SetMoveInput>(HandleInput);
            game.Register<RecallHero>(HandleRecall);
            game.Register<ToggleTeam>(HandleTeam);
            game.Register<SelectHero>(HandleSelect);
            game.Register<CycleLeader>(HandleCycle);
            game.AddSystem(this);
            return this;
        }

        /// <summary>
        /// The player whose leader the followers follow: the one in the game with the lowest id, or none when no one has
        /// joined.
        /// </summary>
        public PlayerId Captain(Game game)
        {
            var captain = PlayerId.None;
            foreach (var p in game.Players)
                if (captain.IsNone || p.Value < captain.Value) captain = p;
            return captain;
        }

        /// <summary>True when a player in <paramref name="game"/> other than <paramref name="except"/> leads hero <paramref name="index"/>.</summary>
        bool LedByAnother(Game game, int index, PlayerId except)
        {
            foreach (var p in game.Players)
                if (p != except && LeaderOf(p) == index) return true;
            return false;
        }

        /// <summary>True when a player in <paramref name="game"/> leads hero <paramref name="index"/>; otherwise it is a follower.</summary>
        public bool IsLed(Game game, int index) => LedByAnother(game, index, PlayerId.None);

        /// <summary>
        /// checkCursorOverHero: the first hero (in party order) the world cursor at (<paramref name="cursorX"/>,
        /// <paramref name="cursorY"/>) is over, or -1. The current leader counts too, as it did in the original.
        /// </summary>
        public int HeroAt(float cursorX, float cursorY)
        {
            var s = Data.Selection;
            var size = Data.HeroSize;
            var cx = cursorX + s.CursorOffset.X;
            var cy = cursorY + s.CursorOffset.Y;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hx = heroes[i].X + size * s.HeroPoint.X + s.HeroOffset.X;
                var hy = heroes[i].Y + size * s.HeroPoint.Y + s.HeroOffset.Y;
                if (WorldCamera.Near(hx, cx, s.Range) && WorldCamera.Near(hy, cy, s.Range)) return i;
            }

            return -1;
        }

        CommandResult HandleTeam(Game game, ToggleTeam command)
        {
            Team = !Team;
            return CommandResult.Accept(new TeamChanged(game.Tick, command.Player, Team));
        }

        /// <summary>
        /// actionHeroSelect in adventure mode: with the team off the old leader kneels; the chosen hero leads and stands.
        /// A hero in battle mode is chosen by the battle's own rule (story A19), so it is refused here.
        /// </summary>
        CommandResult HandleSelect(Game game, SelectHero command)
        {
            if (command.Hero < 0 || command.Hero >= heroes.Count)
                return CommandResult.Reject("There is no hero " + command.Hero + " in the party.");
            var chosen = heroes[command.Hero];
            if (LedByAnother(game, command.Hero, command.Player))
                return CommandResult.Reject(chosen.Name + " is led by another player.");
            if (chosen.Mode == HeroMode.Battle)
                return CommandResult.Reject(chosen.Name + " is in battle; a hero in battle is chosen when it is ready (battle mode).");

            var previous = LeaderOf(command.Player);
            if (!Team)
            {
                var old = heroes[previous];
                old.State = HeroState.Kneeling;
                old.Laying = false;
                old.Kneeling = true;
            }

            leaders[command.Player] = command.Hero;
            chosen.Laying = false;
            chosen.Kneeling = false;
            chosen.State = HeroState.Standing;
            return CommandResult.Accept(new LeaderChanged(game.Tick, command.Player, command.Hero, previous, false));
        }

        /// <summary>
        /// lastHero / nextHero and changeHero: the old leader and the new one (the previous or next hero in party order,
        /// wrapping, passing over any another player leads) both stop running, kneeling and laying and stand.
        /// </summary>
        CommandResult HandleCycle(Game game, CycleLeader command)
        {
            if (command.Step != 1 && command.Step != -1) return CommandResult.Reject("Step to the previous (-1) or next (+1) hero.");
            var previous = LeaderOf(command.Player);
            var next = previous;
            for (var tries = 0; tries < heroes.Count; tries++)
            {
                next = (next + command.Step + heroes.Count) % heroes.Count;
                if (!LedByAnother(game, next, command.Player)) break;
            }

            if (next == previous && heroes.Count > 1) return CommandResult.Reject("Every other hero is led by another player.");
            StandStill(heroes[previous]);
            leaders[command.Player] = next;
            StandStill(heroes[next]);
            return CommandResult.Accept(new LeaderChanged(game.Tick, command.Player, next, previous, true));
        }

        static void StandStill(Hero hero)
        {
            hero.State = HeroState.Standing;
            hero.Running = false;
            hero.Kneeling = false;
            hero.Laying = false;
        }

        /// <summary>
        /// The hero <paramref name="player"/> leads (the original's currHero, per player): player 1 leads Chrono, player 2
        /// Belmont, and so on, until a leader is chosen.
        /// </summary>
        public int LeaderOf(PlayerId player)
        {
            if (leaders.TryGetValue(player, out var index)) return index;
            var place = player.Value - 1;
            return place >= 0 && place < heroes.Count ? place : 0;
        }

        /// <summary>The hero <paramref name="player"/> leads.</summary>
        public Hero Leader(PlayerId player) => heroes[LeaderOf(player)];

        /// <summary>What <paramref name="player"/> is holding now.</summary>
        public MoveInput InputOf(PlayerId player) => inputs.TryGetValue(player, out var input) ? input : default;

        CommandResult HandleInput(Game game, SetMoveInput command)
        {
            inputs[command.Player] = command.Input;
            return CommandResult.Accept();
        }

        CommandResult HandleRecall(Game game, RecallHero command)
        {
            if (command.Hero < 0 || command.Hero >= heroes.Count)
                return CommandResult.Reject("There is no hero " + command.Hero + " in the party.");
            var leader = LeaderOf(command.Player);
            if (command.Hero == leader) return CommandResult.Reject(heroes[leader].Name + " leads; a leader is not recalled to itself.");
            return CommandResult.Accept(Recall(heroes[command.Hero], heroes[leader], game.Tick));
        }

        /// <summary>
        /// recallHero: places <paramref name="hero"/> at its leader's position less half the spread plus the spread times
        /// a random fraction (x first, then y), at the recall depth, from where it falls into place. It is placed, not
        /// moved, so it is drawn there at once rather than eased from where it was.
        /// </summary>
        internal HeroRecalled Recall(Hero hero, Hero leader, long tick)
        {
            var fall = Data.Fall;
            var half = fall.RecallSpread / 2f;
            hero.X = hero.PreviousX = leader.X - half + fall.RecallSpread * (float)random.NextDouble();
            hero.Y = hero.PreviousY = leader.Y - half + fall.RecallSpread * (float)random.NextDouble();
            hero.Z = hero.PreviousZ = fall.RecallZ;
            return new HeroRecalled(tick, hero.Index, leader.Index, hero.X, hero.Y, hero.Z);
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            var now = tick * Clock.TickSeconds;
            var before = new (HeroState, Facing)[heroes.Count];
            for (var i = 0; i < heroes.Count; i++)
            {
                before[i] = (heroes[i].State, heroes[i].Facing);
                heroes[i].PreviousX = heroes[i].X;
                heroes[i].PreviousY = heroes[i].Y;
                heroes[i].PreviousZ = heroes[i].Z;
            }

            // getInput: the input of every player, on the hero each leads.
            var moveHeld = new bool[heroes.Count];
            foreach (var player in game.Players)
            {
                var input = InputOf(player);
                var hero = Leader(player);
                ApplyInput(hero, input);
                if (input.Move) moveHeld[hero.Index] = true;
            }

            var led = new bool[heroes.Count];
            for (var i = 0; i < heroes.Count; i++) led[i] = IsLed(game, i);
            var captain = Captain(game);
            var leader = captain.IsNone ? null : Leader(captain);

            Flock(led);

            // updateHero: every hero's AI, then its move.
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                switch (hero.State)
                {
                    case HeroState.Kneeling:
                        Kneel(hero);
                        break;
                    case HeroState.Standing:
                        Stand(hero);
                        break;
                    case HeroState.Walking:
                        Accelerate(hero, hero.Definition.WalkSpeed, moveHeld[i]);
                        Animate(hero, now, hero.Definition.WalkRate, Cycle(hero, Sprites.Walk), hero.Definition.WalkFrames);
                        break;
                    case HeroState.Running:
                        Accelerate(hero, hero.Definition.RunSpeed, moveHeld[i]);
                        Animate(hero, now, hero.Definition.RunRate, Cycle(hero, Sprites.Run), hero.Definition.RunFrames);
                        break;
                    default:
                        // Resting, Drawing and Battle hold still; their pictures come with their stories.
                        hero.DX = 0f;
                        hero.DY = 0f;
                        break;
                }

                // checkfollow, then checkWall and checkFall.
                if (!led[i] && leader != null) Follow(hero, leader, tick, events);
                if (Walls != null) WallCollision.Apply(hero, Walls, Data.HeroSize);
                if (Fall(hero)) events.Add(new HeroLanded(tick, i));

                hero.X += hero.DX * Tuning.GameSpeed;
                hero.Y += hero.DY * Tuning.GameSpeed;

                if (before[i].Item1 != hero.State || before[i].Item2 != hero.Facing)
                    events.Add(new HeroStateChanged(tick, i, hero.State, hero.Facing));
            }
        }

        // ---- following (Heroes.checkfollow, checkFlocking) ---------------------------------------------------------

        /// <summary>
        /// checkfollow for a follower: with the team off it kneels (laying cleared). With it on, and the follower not in
        /// battle: beyond the recall range on either axis it is recalled beside the leader; then, by the nearest range it
        /// is within on both axes, it runs, walks or stands, heading for the leader at that speed times the distance, and
        /// faces the way it heads (dy's sign, unless dx is the larger).
        /// </summary>
        void Follow(Hero hero, Hero leader, long tick, ICollection<IGameEvent> events)
        {
            if (!Team)
            {
                hero.State = HeroState.Kneeling;
                hero.Laying = false;
                hero.Kneeling = true;
                return;
            }

            if (hero.Mode == HeroMode.Battle) return;
            var f = Data.Follow;
            if (!WorldCamera.Near(leader.X, hero.X, f.RecallRange) || !WorldCamera.Near(leader.Y, hero.Y, f.RecallRange))
                events.Add(Recall(hero, leader, tick));

            var speed = 0f;
            if (Within(leader, hero, f.RecallRange))
            {
                hero.State = HeroState.Running;
                speed = f.RunSpeed;
            }

            if (Within(leader, hero, f.WalkRange))
            {
                hero.State = HeroState.Walking;
                speed = f.WalkSpeed;
            }

            if (Within(leader, hero, f.StandRange))
            {
                hero.State = HeroState.Standing;
                speed = 0f;
            }

            var dx = leader.X - hero.X;
            var dy = leader.Y - hero.Y;
            hero.Facing = dy > 0 ? Facing.Up : Facing.Down;
            if (Math.Abs(dx) > Math.Abs(dy)) hero.Facing = dx > 0 ? Facing.Right : Facing.Left;
            hero.DY = dy * speed;
            hero.DX = dx * speed;
        }

        static bool Within(Hero a, Hero b, float range) => WorldCamera.Near(a.X, b.X, range) && WorldCamera.Near(a.Y, b.Y, range);

        /// <summary>
        /// checkFlocking: every ordered pair of followers (the original's two loops, a hero paired with itself too, which
        /// nudges it by nothing), both moving, both alive, within the flocking range on both axes: each is pushed along x
        /// by its own |dY| / divisor and along y by its own |dX| / divisor, away from the other.
        /// </summary>
        internal void Flock(bool[] led)
        {
            var fl = Data.Flocking;
            for (var d = 0; d < heroes.Count; d++)
            {
                for (var c = 0; c < heroes.Count; c++)
                {
                    if (led[d] || led[c]) continue;
                    var a = heroes[d];
                    var b = heroes[c];
                    if (a.State == HeroState.Standing || b.State == HeroState.Standing) continue;
                    if (!Within(a, b, fl.Range)) continue;
                    if (a.Health <= 0 || b.Health <= 0) continue;

                    if (a.X < b.X)
                    {
                        a.X += -Math.Abs(a.DY) / fl.Divisor;
                        b.X += Math.Abs(b.DY) / fl.Divisor;
                    }
                    else
                    {
                        a.X += Math.Abs(a.DY) / fl.Divisor;
                        b.X += -Math.Abs(b.DY) / fl.Divisor;
                    }

                    if (a.Y < b.Y)
                    {
                        a.Y += -Math.Abs(a.DX) / fl.Divisor;
                        b.Y += Math.Abs(b.DX) / fl.Divisor;
                    }
                    else
                    {
                        a.Y += Math.Abs(a.DX) / fl.Divisor;
                        b.Y += -Math.Abs(b.DX) / fl.Divisor;
                    }
                }
            }
        }

        // ---- input (Input.getKeyboard, getMouse, KeysUp) ----------------------------------------------------------

        void ApplyInput(Hero hero, MoveInput input)
        {
            // actionRun: running needs stamina.
            if (input.Run) hero.Running = hero.Stamina > 0;

            // actionLeft / Right / Up / Down, in that order, so the last held arrow sets the facing.
            if (input.Left) Arrow(hero, Facing.Left);
            if (input.Right) Arrow(hero, Facing.Right);
            if (input.Up) Arrow(hero, Facing.Up);
            if (input.Down) Arrow(hero, Facing.Down);

            // tryingConflict: opposite arrows together face Down and stand.
            if ((input.Left && input.Right) || (input.Up && input.Down))
            {
                hero.Facing = Facing.Down;
                hero.State = HeroState.Standing;
            }

            if (input.Move) MoveTowardCursor(hero, input.CursorX, input.CursorY);

            // KeysUp: released keys clear their flags; with nothing held the hero stands.
            if (!input.Run) hero.Running = false;
            if (!input.Move)
            {
                if (!input.Left) hero.MoveLeft = false;
                if (!input.Right) hero.MoveRight = false;
                if (!input.Up) hero.MoveUp = false;
                if (!input.Down) hero.MoveDown = false;
            }

            if (!input.AnyArrow && !input.Move && !hero.Kneeling && !hero.Laying &&
                hero.State != HeroState.Drawing && hero.State != HeroState.Battle)
                hero.State = HeroState.Standing;
        }

        static void Arrow(Hero hero, Facing facing)
        {
            switch (facing)
            {
                case Facing.Left: hero.MoveLeft = true; break;
                case Facing.Right: hero.MoveRight = true; break;
                case Facing.Up: hero.MoveUp = true; break;
                default: hero.MoveDown = true; break;
            }

            if (hero.Laying) return;
            hero.Kneeling = false;
            hero.Facing = facing;

            // checkRunOrWalk
            hero.State = hero.Running ? HeroState.Running : HeroState.Walking;
            if (hero.Burdened) hero.State = HeroState.Standing;
        }

        /// <summary>
        /// actionMove: while the move button is held the hero heads for the cursor, running when it is far (or the run
        /// key is held) and it has stamina, walking when near, faster in the last 0.1 and stopped within 0.01.
        /// </summary>
        void MoveTowardCursor(Hero hero, float cursorX, float cursorY)
        {
            if (hero.Burdened || hero.Kneeling || hero.Laying) return;
            var t = Tuning;
            var size = Data.HeroSize;
            var targetX = cursorX + t.CursorOffset.X;
            var targetY = cursorY + t.CursorOffset.Y;
            var fromX = hero.X + size * t.HeroPoint.X;
            var fromY = hero.Y + size * t.HeroPoint.Y;

            var dx = Clamp(targetX - fromX, t.MaxReach);
            var dy = Clamp(targetY - fromY, t.MaxReach);

            if ((Math.Abs(dx) >= t.RunReach || Math.Abs(dy) >= t.RunReach || hero.Running) && hero.Stamina > 0)
            {
                hero.State = HeroState.Running;
                hero.Running = true;
            }
            else
            {
                hero.State = HeroState.Walking;
                hero.Running = false;
            }

            // Facing: from the sign of y, unless x is the larger.
            hero.Facing = dy > 0 ? Facing.Up : Facing.Down;
            if (Math.Abs(dx) > Math.Abs(dy)) hero.Facing = dx > 0 ? Facing.Right : Facing.Left;

            if (hero.State == HeroState.Walking)
            {
                dx = Clamp(dx, t.RunReach);
                dy = Clamp(dy, t.RunReach);
            }

            var vx = dx * t.FollowSpeed;
            var vy = dy * t.FollowSpeed;

            if (WorldCamera.Near(fromX, targetX, t.NearRange) && WorldCamera.Near(fromY, targetY, t.NearRange))
            {
                vx *= t.NearBoost;
                vy *= t.NearBoost;
            }

            if (WorldCamera.Near(fromX, targetX, t.ArriveRange) && WorldCamera.Near(fromY, targetY, t.ArriveRange))
            {
                hero.State = HeroState.Standing;
                vx = 0f;
                vy = 0f;
            }

            hero.DX = vx;
            hero.DY = vy;
        }

        /// <summary>
        /// checkFall: a hero below the ground rises by the fall's rise and shows the halt_left picture; at or above it, it
        /// is set on the ground. True on the tick its rise reaches the ground (the next tick sets it exactly to 0).
        /// </summary>
        bool Fall(Hero hero)
        {
            if (hero.Z < 0f)
            {
                hero.Z += Data.Fall.Rise;
                hero.Sprite = Sprites.Halt(Facing.Left);
                return hero.Z >= 0f;
            }

            hero.Z = 0f;
            return false;
        }

        static float Clamp(float value, float limit) => value > limit ? limit : value < -limit ? -limit : value;

        // ---- AI (Heroes.aiStanding, aiWalking, aiRunning, AnimateHero) ---------------------------------------------

        void Stand(Hero hero)
        {
            hero.DX = 0f;
            hero.DY = 0f;
            hero.Frame = 0;
            hero.Sprite = Sprites.Stand(hero.Facing);
        }

        /// <summary>aiKneeling: still, the kneeling picture of the facing, or the laid-down one while laying.</summary>
        void Kneel(Hero hero)
        {
            hero.DX = 0f;
            hero.DY = 0f;
            hero.Sprite = hero.Laying ? Sprites.Sleep(hero.Facing) : Sprites.Kneel(hero.Facing);
        }

        /// <summary>
        /// Each held direction moves the velocity toward +/-speed by the acceleration step, from above or below (so it
        /// settles within one step of it); an axis with no direction held stops at once, unless the move button is held
        /// (the cursor set the velocity).
        /// </summary>
        void Accelerate(Hero hero, float speed, bool moveHeld)
        {
            var step = (double)Tuning.Acceleration;
            var dx = hero.DX;
            if (hero.MoveLeft) dx = Toward(dx, -speed, step);
            else if (hero.MoveRight) dx = Toward(dx, speed, step);
            else if (!moveHeld) dx = 0f;

            var dy = hero.DY;
            if (hero.MoveDown) dy = Toward(dy, -speed, step);
            else if (hero.MoveUp) dy = Toward(dy, speed, step);
            else if (!moveHeld) dy = 0f;

            hero.DX = dx;
            hero.DY = dy;
        }

        /// <summary>
        /// The original's two tests: below the target, add a step; then, above it, take one away. The arithmetic is the
        /// original's (a double step on a single-precision velocity).
        /// </summary>
        static float Toward(float velocity, float target, double step)
        {
            if (velocity < target) velocity = (float)(velocity + step);
            if (velocity > target) velocity = (float)(velocity - step);
            return velocity;
        }

        /// <summary>
        /// The cycle the facing shows: facing up or down while a side arrow is held, the side's cycle (the original drew
        /// left, then right over it, so right wins when both are held).
        /// </summary>
        static IReadOnlyList<string> Cycle(Hero hero, Func<Facing, IReadOnlyList<string>> set)
        {
            if ((hero.Facing == Facing.Up || hero.Facing == Facing.Down) && (hero.MoveLeft || hero.MoveRight))
                return set(hero.MoveRight ? Facing.Right : Facing.Left);
            return set(hero.Facing);
        }

        /// <summary>AnimateHero: once <paramref name="rate"/> seconds have passed, the next frame, looping to the first.</summary>
        static void Animate(Hero hero, double now, float rate, IReadOnlyList<string> frames, int maxFrame)
        {
            if (hero.Frame < 0 || hero.Frame > maxFrame || hero.Frame >= frames.Count) hero.Frame = 0;

            // The tick clock is exact; the epsilon keeps 6 ticks from falling a hair short of 0.1 s.
            if (now + 1e-6 >= hero.LastFrameTime + rate)
            {
                hero.Frame = hero.Frame < maxFrame ? hero.Frame + 1 : 0;
                hero.LastFrameTime = now;
            }

            hero.Sprite = frames[hero.Frame];
        }
    }
}
