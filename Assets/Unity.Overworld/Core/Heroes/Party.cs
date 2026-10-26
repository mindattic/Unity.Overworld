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
    /// velocity times the game speed. Before its AI each hero's status is checked and it regenerates (checkStatus,
    /// checkRegen; U.OVW-US-A10, RECONSTRUCTION 7), and after its move its status animation steps (renderIcon).
    /// Installed on a <see cref="Game"/>, which then accepts <see cref="SetMoveInput"/>, <see cref="RecallHero"/>,
    /// <see cref="ToggleTeam"/>, <see cref="SelectHero"/>, <see cref="CycleLeader"/>, <see cref="SetHeroVitals"/> and
    /// <see cref="Rest"/> (a laid hero chooses its sleep's hours with the arrows; <see cref="SleepCycle"/> sleeps);
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
            if (data.Sprites == null || data.Locomotion == null || data.Fall == null || data.Follow == null || data.Flocking == null || data.Selection == null ||
                data.Vitals == null || data.Rest == null)
                throw new ArgumentException("The hero data has no sprites, locomotion, fall, follow, flocking, selection, vitals or rest.", nameof(data));
            Walls = walls;
            this.random = random ?? new Random();
            Team = data.Follow.TeamAtStart;
            for (var i = 0; i < data.Heroes.Count; i++)
            {
                var hero = new Hero(i, data.Heroes[i]);
                hero.Sprite = data.Sprites.Stand(hero.Facing);
                hero.SleepHours = data.Rest.MinHours;
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
            game.Register<SetHeroVitals>(HandleVitals);
            game.Register<Rest>(HandleRest);
            game.Register<PlaceHero>(HandlePlace);
            game.AddSystem(this);
            return this;
        }

        /// <summary>CHANGE HERO X / Y: the hero is placed at the point (and drawn there at once, not eased from where it was).</summary>
        CommandResult HandlePlace(Game game, PlaceHero command)
        {
            if (command.Hero < 0 || command.Hero >= heroes.Count)
                return CommandResult.Reject("There is no hero " + command.Hero + " in the party.");
            if (float.IsNaN(command.X) || float.IsNaN(command.Y) || float.IsInfinity(command.X) || float.IsInfinity(command.Y))
                return CommandResult.Reject("A hero is placed at a finite point.");
            return CommandResult.Accept(Place(command.Hero, command.X, command.Y, game.Tick));
        }

        /// <summary>CHANGE HERO X / Y: hero <paramref name="index"/> is placed at (x, y), drawn there at once. The hero and point must be valid.</summary>
        internal HeroPlaced Place(int index, float x, float y, long tick)
        {
            var hero = heroes[index];
            hero.X = hero.PreviousX = x;
            hero.Y = hero.PreviousY = y;
            return new HeroPlaced(tick, hero.Index, hero.X, hero.Y);
        }

        /// <summary>
        /// While true for a player (its script console is open or one of its scripts runs; RECONSTRUCTION 9.3), the party
        /// ignores what that player holds, as the original stopped reading game input: its leader keeps whatever a script
        /// set it doing. Null: never.
        /// </summary>
        public Func<PlayerId, bool> InputLocked { get; set; }

        bool Locked(PlayerId player) => InputLocked != null && InputLocked(player);

        /// <summary>
        /// actionRest: not in battle mode, the leader stops running, then a standing hero kneels, a kneeling one lies down
        /// and a laid one kneels again; either way its AI is Kneeling. (A hero laid by a draught without kneeling matches
        /// none of the three and only kneels its AI, as in the original.)
        /// </summary>
        CommandResult HandleRest(Game game, Rest command)
        {
            var hero = Leader(command.Player);
            if (hero.Mode == HeroMode.Battle) return CommandResult.Reject(hero.Name + " is in battle; a hero rests in adventure mode.");
            if (QuestScreenOpenFor(command.Player)) return CommandResult.Reject("The quest screen is open; the leader rests once it is shut.");
            hero.Running = false;
            var events = new List<IGameEvent>();
            // actionRest shuts the pack.
            Rested?.Invoke(command.Player, game.Tick, events);
            if (!hero.Kneeling && !hero.Laying) hero.Kneeling = true;
            else if (hero.Kneeling && !hero.Laying) hero.Laying = true;
            else if (hero.Kneeling && hero.Laying) hero.Laying = false;
            hero.State = HeroState.Kneeling;
            events.Add(new HeroRestChanged(game.Tick, hero.Index, hero.Kneeling, hero.Laying));
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>Called when a player's leader rests (the <see cref="InventoryControl"/> shuts that player's pack, as actionRest did).</summary>
        internal Action<PlayerId, long, ICollection<IGameEvent>> Rested { get; set; }

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

        /// <summary>What <paramref name="player"/> is holding now (nothing while its input is locked).</summary>
        public MoveInput InputOf(PlayerId player) => !Locked(player) && inputs.TryGetValue(player, out var input) ? input : default;

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
                // getInput was not called while the console was open or a script ran (not even its KeysUp).
                if (Locked(player)) continue;
                var input = InputOf(player);
                var hero = Leader(player);
                var focus = Focus != null ? Focus(player) : InputFocus.World;
                var questScreen = QuestScreenOpenFor(player);
                ApplyInput(hero, input, focus, questScreen);
                if (input.Move && focus != InputFocus.Inventory && !questScreen) moveHeld[hero.Index] = true;
            }

            var led = new bool[heroes.Count];
            for (var i = 0; i < heroes.Count; i++) led[i] = IsLed(game, i);
            var captain = Captain(game);
            var leader = captain.IsNone ? null : Leader(captain);

            Flock(led);

            // updateHero: every hero's status and regeneration, its AI, then its move.
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                var vitalsBefore = (hero.Health, hero.Mana, hero.Stamina);
                var statusBefore = hero.Status;
                CheckStatus(hero);
                CheckWeight(hero, tick, events);
                Regenerate(hero, now);

                switch (hero.State)
                {
                    case HeroState.Kneeling:
                        Kneel(hero, now, tick, events);
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

                // renderIcon: the status animation above the hero.
                AnimateStatus(hero, now);
                if (hero.Status != statusBefore) events.Add(new HeroStatusChanged(tick, i, hero.Status, statusBefore));
                if ((hero.Health, hero.Mana, hero.Stamina) != vitalsBefore) events.Add(new HeroVitalsChanged(tick, i, hero.Health, hero.Mana, hero.Stamina));
            }
        }

        // ---- vitals (Heroes.checkStatus, checkRegen; StatusIcon.renderIcon) ----------------------------------------

        VitalsTuning Vitals => Data.Vitals;

        /// <summary>
        /// The tuning's fractions as the decimals the data wrote (0.15, not the float's 0.1500000059...), so a threshold
        /// lands where the original's double arithmetic put it: 0.15 x 60 is 9, and stamina 9 is not Exhausted.
        /// </summary>
        static double Fraction(float value) => Math.Round((double)value, 6);

        /// <summary>
        /// checkStatus: a hero not poisoned is Fine, Exhausted with stamina below 15 percent of its maximum, Angry (over
        /// Exhausted) with health below 5 percent; a poisoned one stays Poisoned until it is cured.
        /// </summary>
        void CheckStatus(Hero hero)
        {
            if (hero.Status == HeroStatus.Poisoned) return;
            var status = HeroStatus.Fine;
            if (hero.Stamina < Fraction(Vitals.ExhaustedBelow) * hero.MaxStamina) status = HeroStatus.Exhausted;
            if (hero.Health < Fraction(Vitals.AngryBelow) * hero.MaxHealth) status = HeroStatus.Angry;
            hero.Status = status;
        }

        /// <summary>
        /// checkRegen in adventure mode: once the hero's regen rate (2 s) has passed since its last step, a hero not
        /// poisoned gains stamina kneeling (+2, unless picking up) or standing (+1), up to its maximum; a poisoned one loses
        /// a health point instead (to 0); and a running hero loses a stamina point (to 0) either way. Battle mode's refill
        /// is the battle story's (A18).
        /// </summary>
        void Regenerate(Hero hero, double now)
        {
            if (hero.Mode != HeroMode.Adventure) return;
            // The tick clock is exact; the epsilon keeps 120 ticks from falling a hair short of 2 s.
            if (now + 1e-6 < hero.LastRegenTime + hero.Definition.RegenRate) return;
            var v = Vitals;
            if (hero.Status != HeroStatus.Poisoned)
            {
                if (hero.State == HeroState.Kneeling && !hero.PickingUp) hero.Stamina = Math.Min(hero.MaxStamina, hero.Stamina + v.KneelingStamina);
                if (hero.State == HeroState.Standing) hero.Stamina = Math.Min(hero.MaxStamina, hero.Stamina + v.StandingStamina);
            }
            else
            {
                hero.Health = Math.Max(0, hero.Health - v.PoisonHealth);
            }

            if (hero.State == HeroState.Running) hero.Stamina = Math.Max(0, hero.Stamina - v.RunningStamina);
            hero.LastRegenTime = now;
        }

        /// <summary>
        /// AnimateIcon: the status's frames advance every icon rate (0.3 s), looping; a Fine hero shows none. Each hero keeps
        /// its own timer (the original shared one, so only the first animated hero ever moved; BIBLE).
        /// </summary>
        void AnimateStatus(Hero hero, double now)
        {
            var frames = Vitals.Icon(hero.Status);
            if (frames.Count == 0)
            {
                hero.StatusIcon = null;
                return;
            }

            if (hero.StatusFrame < 0 || hero.StatusFrame >= frames.Count) hero.StatusFrame = 0;
            if (now + 1e-6 >= hero.LastStatusTime + Vitals.IconRate)
            {
                hero.StatusFrame = hero.StatusFrame < frames.Count - 1 ? hero.StatusFrame + 1 : 0;
                hero.LastStatusTime = now;
            }

            hero.StatusIcon = frames[hero.StatusFrame];
        }

        /// <summary>CHANGE HERO HP / MP / AP / STATUS as assignments: each given value clamped to 0 and the hero's maximum.</summary>
        CommandResult HandleVitals(Game game, SetHeroVitals command)
        {
            if (command.Hero < 0 || command.Hero >= heroes.Count)
                return CommandResult.Reject("There is no hero " + command.Hero + " in the party.");
            var events = new List<IGameEvent>();
            SetVitals(command.Hero, command.Health, command.Mana, command.Stamina, command.Status, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>
        /// CHANGE HERO HP / MP / AP / STATUS as assignments (<see cref="SetHeroVitals"/>, and the item scripts): each given
        /// value clamped to 0 and the hero's maximum; adds the vitals (and any status change) to <paramref name="events"/>.
        /// </summary>
        internal void SetVitals(int index, int? health, int? mana, int? stamina, HeroStatus? status, long tick, ICollection<IGameEvent> events)
        {
            var hero = heroes[index];
            var statusBefore = hero.Status;
            if (health.HasValue) hero.Health = Math.Max(0, Math.Min(hero.MaxHealth, health.Value));
            if (mana.HasValue) hero.Mana = Math.Max(0, Math.Min(hero.MaxMana, mana.Value));
            if (stamina.HasValue) hero.Stamina = Math.Max(0, Math.Min(hero.MaxStamina, stamina.Value));
            if (status.HasValue) hero.Status = status.Value;
            events.Add(new HeroVitalsChanged(tick, hero.Index, hero.Health, hero.Mana, hero.Stamina));
            if (hero.Status != statusBefore) events.Add(new HeroStatusChanged(tick, hero.Index, hero.Status, statusBefore));
        }

        // ---- weight (Heroes.checkWeight) -------------------------------------------------------------------------

        /// <summary>The party's pack, whose weight against <see cref="HeroData.PartyWeightLimit"/> burdens every hero (null: never burdened).</summary>
        public Inventory Pack { get; set; }

        /// <summary>
        /// checkWeight: with the pack heavier than the party's limit the hero is burdened and kneels (so it cannot walk,
        /// run or head for the cursor); otherwise it is not burdened (and kneels on until it moves).
        /// </summary>
        void CheckWeight(Hero hero, long tick, ICollection<IGameEvent> events)
        {
            var burdened = Pack != null && Pack.Weight > Data.PartyWeightLimit;
            if (burdened)
            {
                hero.Kneeling = true;
                hero.State = HeroState.Kneeling;
            }

            if (burdened == hero.Burdened) return;
            hero.Burdened = burdened;
            events.Add(new HeroBurdened(tick, hero.Index, burdened));
        }

        // ---- what the keys work on (Inventory.bInventory, HeroSelect.bHeroSelect) ---------------------------------

        /// <summary>
        /// What a player's keys work on now: with the pack open the arrows move its cursor, the run key swaps and the move
        /// button drops (the leader neither turns, walks, runs nor heads for the cursor); while an item's target is chosen
        /// left and right choose it (up, down and the move button still move the leader, as they did). Null: the world.
        /// </summary>
        public Func<PlayerId, InputFocus> Focus { get; set; }

        /// <summary>
        /// True while a player's quest screen is open (bQuestScreen): its arrows only mark the direction held (the screen
        /// takes them), the move button does not head for the cursor and the leader does not rest; the run key and the
        /// pick-up button still work, as they did. Null: never open.
        /// </summary>
        public Func<PlayerId, bool> QuestScreenOpen { get; set; }

        bool QuestScreenOpenFor(PlayerId player) => QuestScreenOpen != null && QuestScreenOpen(player);

        /// <summary>The party's experience (Root.Experience): the quests' rewards add to it; nothing in the original spent it.</summary>
        public int Experience { get; internal set; }

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

        void ApplyInput(Hero hero, MoveInput input, InputFocus focus, bool questScreen)
        {
            var packOpen = focus == InputFocus.Inventory;
            var choosing = focus == InputFocus.Target;

            // actionRun: running needs stamina (with the pack open the run key swaps instead).
            if (input.Run && !packOpen) hero.Running = hero.Stamina > 0;

            // actionLeft / Right / Up / Down, in that order, so the last held arrow sets the facing; while the pick-up
            // button is held (bItemPickup) they only mark the direction held, as they do while the pack or the quest
            // screen is open, and left and right do while an item's target is chosen.
            var held = input.PickUp || packOpen || questScreen;
            if (input.Left) Arrow(hero, Facing.Left, held || choosing);
            if (input.Right) Arrow(hero, Facing.Right, held || choosing);
            if (input.Up) Arrow(hero, Facing.Up, held);
            if (input.Down) Arrow(hero, Facing.Down, held);

            // tryingConflict: opposite arrows together face Down and stand.
            if ((input.Left && input.Right) || (input.Up && input.Down))
            {
                hero.Facing = Facing.Down;
                hero.State = HeroState.Standing;
            }

            // actionMove waits while the pick-up button is held (getMouse tests the left button first; Enter blocks it);
            // with the pack open the move button drops instead, and with the quest screen open it does nothing.
            if (input.Move && !input.PickUp && !packOpen && !questScreen) MoveTowardCursor(hero, input.CursorX, input.CursorY);

            // KeysUp: released keys clear their flags; with nothing held (the pick-up button included, and no pickup
            // under way) the hero stands.
            if (!input.Run) hero.Running = false;
            if (!input.Move)
            {
                if (!input.Left) hero.MoveLeft = false;
                if (!input.Right) hero.MoveRight = false;
                if (!input.Up) hero.MoveUp = false;
                if (!input.Down) hero.MoveDown = false;
            }

            if (!input.AnyArrow && !input.Move && !input.PickUp && !hero.Kneeling && !hero.Laying && !hero.PickingUp &&
                hero.State != HeroState.Drawing && hero.State != HeroState.Battle)
                hero.State = HeroState.Standing;
        }

        static void Arrow(Hero hero, Facing facing, bool pickUpHeld)
        {
            switch (facing)
            {
                case Facing.Left: hero.MoveLeft = true; break;
                case Facing.Right: hero.MoveRight = true; break;
                case Facing.Up: hero.MoveUp = true; break;
                default: hero.MoveDown = true; break;
            }

            if (hero.Laying || pickUpHeld) return;
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

        /// <summary>
        /// aiKneeling: still, the kneeling picture of the facing, or the laid-down one while laying; laid, once more than
        /// the chooser's rate has passed since its last check, a held left or down arrow takes an hour off its sleep and
        /// right or up adds one, within the fewest and most hours.
        /// </summary>
        void Kneel(Hero hero, double now, long tick, ICollection<IGameEvent> events)
        {
            hero.DX = 0f;
            hero.DY = 0f;
            hero.Sprite = hero.Laying ? Sprites.Sleep(hero.Facing) : Sprites.Kneel(hero.Facing);
            if (!hero.Laying) return;

            var rest = Data.Rest;
            if (!(now > hero.LastSleepTime + rest.ChooserRate)) return;
            var hours = hero.SleepHours;
            if (hero.MoveLeft || hero.MoveDown) hours = Math.Max(rest.MinHours, hours - 1);
            if (hero.MoveRight || hero.MoveUp) hours = Math.Min(rest.MaxHours, hours + 1);
            hero.LastSleepTime = now;
            if (hours == hero.SleepHours) return;
            hero.SleepHours = hours;
            events.Add(new SleepHoursChanged(tick, hero.Index, hours));
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
