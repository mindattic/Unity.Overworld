using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The party in play and its locomotion (U.OVW-US-A5; RECONSTRUCTION 4.3, 4.4, 5.1, 5.4 and 5.6). Each tick it does
    /// what the original's frame did: every player's held input is applied to the hero that player leads (getKeyboard's
    /// arrows and run key, getMouse's move-to-cursor, then KeysUp), then every hero runs its AI state (aiStanding,
    /// aiWalking, aiRunning: the velocity steps toward its speed and the walk or run cycle advances on the clock) and
    /// moves by its velocity times the game speed. Installed on a <see cref="Game"/>, which then accepts
    /// <see cref="SetMoveInput"/>; presentation reads <see cref="Heroes"/>.
    /// </summary>
    public sealed class Party : ITickSystem
    {
        readonly List<Hero> heroes = new List<Hero>();
        readonly Dictionary<PlayerId, MoveInput> inputs = new Dictionary<PlayerId, MoveInput>();
        readonly Dictionary<PlayerId, int> leaders = new Dictionary<PlayerId, int>();

        public Party(HeroData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.Sprites == null || data.Locomotion == null) throw new ArgumentException("The hero data has no sprites or locomotion.", nameof(data));
            for (var i = 0; i < data.Heroes.Count; i++)
            {
                var hero = new Hero(i, data.Heroes[i]);
                hero.Sprite = data.Sprites.Stand(hero.Facing);
                heroes.Add(hero);
            }
        }

        public HeroData Data { get; }
        public IReadOnlyList<Hero> Heroes => heroes;
        LocomotionTuning Tuning => Data.Locomotion;
        HeroSprites Sprites => Data.Sprites;

        /// <summary>Registers <see cref="SetMoveInput"/> and the per-tick rules on <paramref name="game"/>.</summary>
        public Party Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<SetMoveInput>(HandleInput);
            game.AddSystem(this);
            return this;
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

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            var now = tick * Clock.TickSeconds;
            var before = new (HeroState, Facing)[heroes.Count];
            for (var i = 0; i < heroes.Count; i++)
            {
                before[i] = (heroes[i].State, heroes[i].Facing);
                heroes[i].PreviousX = heroes[i].X;
                heroes[i].PreviousY = heroes[i].Y;
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

            // updateHero: every hero's AI, then its move.
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                switch (hero.State)
                {
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
                        // Kneeling, Resting, Drawing and Battle hold still; their pictures come with their stories.
                        hero.DX = 0f;
                        hero.DY = 0f;
                        break;
                }

                hero.X += hero.DX * Tuning.GameSpeed;
                hero.Y += hero.DY * Tuning.GameSpeed;

                if (before[i].Item1 != hero.State || before[i].Item2 != hero.Facing)
                    events.Add(new HeroStateChanged(tick, i, hero.State, hero.Facing));
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
