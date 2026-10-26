using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// One party hero in play (the original's aHero): where it is, how fast it is going, which way it faces, its AI
    /// state, the flags the input sets, and the animation frame it shows. Changed only by <see cref="Party"/>'s rules.
    /// </summary>
    public sealed class Hero
    {
        internal Hero(int index, HeroDefinition definition)
        {
            Index = index;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            X = PreviousX = definition.Start.X;
            Y = PreviousY = definition.Start.Y;
            Facing = definition.Facing;
            State = definition.State;
            Mode = definition.Mode;
            Stamina = definition.Stamina;
            Health = definition.Health;
            Mana = definition.Mana;
            Status = HeroStatus.Fine;
        }

        /// <summary>The hero's place in the party (0 Chrono, 1 Belmont, 2 Flagg).</summary>
        public int Index { get; }

        public HeroDefinition Definition { get; }
        public string Name => Definition.Name;

        /// <summary>Position in world units: the quad's left edge and vertical centre.</summary>
        public float X { get; internal set; }

        public float Y { get; internal set; }

        /// <summary>Depth: 0 on the ground; below 0 the hero is falling into place (it rises back to 0, checkFall).</summary>
        public float Z { get; internal set; }

        /// <summary>The hero is falling into place (below the ground plane, rising to it).</summary>
        public bool Falling => Z < 0f;

        /// <summary>Where the hero was at the start of the last tick, for presentation to interpolate.</summary>
        public float PreviousX { get; internal set; }

        public float PreviousY { get; internal set; }
        public float PreviousZ { get; internal set; }

        /// <summary>Velocity, world units per tick.</summary>
        public float DX { get; internal set; }

        public float DY { get; internal set; }

        public Facing Facing { get; internal set; }
        public HeroState State { get; internal set; }
        public HeroMode Mode { get; internal set; }
        /// <summary>Stamina (AP): running needs it, and it is the hero's turn gauge in battle.</summary>
        public int Stamina { get; internal set; }

        /// <summary>Health (HP); a hero at 0 does not flock (checkFlocking).</summary>
        public int Health { get; internal set; }

        /// <summary>Mana (MP).</summary>
        public int Mana { get; internal set; }

        public int MaxHealth => Definition.MaxHealth;
        public int MaxMana => Definition.MaxMana;
        public int MaxStamina => Definition.MaxStamina;

        /// <summary>Exhausted, Fine, Angry or Poisoned (checkStatus); Poisoned stays until it is cured.</summary>
        public HeroStatus Status { get; internal set; }

        /// <summary>The frame of the status animation shown above the hero (0-based).</summary>
        public int StatusFrame { get; internal set; }

        /// <summary>The status picture shown above the hero, or null while it is Fine.</summary>
        public string StatusIcon { get; internal set; }

        /// <summary>When the status animation last advanced, in game seconds.</summary>
        internal double LastStatusTime { get; set; }

        /// <summary>When the hero last regenerated, in game seconds (lastRegenCheck).</summary>
        internal double LastRegenTime { get; set; }

        /// <summary>
        /// The hero is kneeling to pick something up (no stamina comes back meanwhile): set when the pick-up button is
        /// pressed, cleared when the pickup completes or the button is let go (<see cref="Pickups"/>).
        /// </summary>
        public bool PickingUp { get; internal set; }

        /// <summary>The run key is held and the hero had stamina (or the mouse sent it running this tick).</summary>
        public bool Running { get; internal set; }

        public bool Kneeling { get; internal set; }

        /// <summary>Lying down (the rest key's second press, or a sleeping draught): the sleep picture and the hours chooser.</summary>
        public bool Laying { get; internal set; }

        public bool Burdened { get; internal set; }

        /// <summary>
        /// The hours a sleep will last (the original's sleepHours, one per player here: the chooser of the hero a player
        /// leads), set with the arrows while laid and back to the fewest after each sleep.
        /// </summary>
        public int SleepHours { get; internal set; } = 1;

        /// <summary>When the hours chooser last checked the arrows, in game seconds (lastSleepCheck).</summary>
        internal double LastSleepTime { get; set; }

        /// <summary>The held-direction flags (moveLeft/Right/Up/Down): kept while the move button is held.</summary>
        public bool MoveLeft { get; internal set; }

        public bool MoveRight { get; internal set; }
        public bool MoveUp { get; internal set; }
        public bool MoveDown { get; internal set; }

        /// <summary>The frame of the current animation (0-based) and the picture it shows.</summary>
        public int Frame { get; internal set; }

        public string Sprite { get; internal set; }

        /// <summary>When the frame last advanced, in game seconds (lastHeroCheck).</summary>
        internal double LastFrameTime { get; set; }

        public override string ToString() => Name + " " + State + " " + Facing + " at (" + X + ", " + Y + ")";
    }

    /// <summary>
    /// What one player is holding this tick: the arrows, the run key (Right Ctrl or Space), the move button (right
    /// mouse) and where the world cursor is. Input sends it every frame; it stays until the next one.
    /// </summary>
    public readonly struct MoveInput : IEquatable<MoveInput>
    {
        public MoveInput(bool left, bool right, bool up, bool down, bool run, bool move, float cursorX, float cursorY, bool pickUp = false)
        {
            Left = left;
            Right = right;
            Up = up;
            Down = down;
            Run = run;
            Move = move;
            CursorX = cursorX;
            CursorY = cursorY;
            PickUp = pickUp;
        }

        /// <summary>
        /// The pick-up button (Enter, or the left mouse button off the team button) is held (actionPickup): the leader kneels
        /// and picks up the selected item in reach; while it is held the arrows and the move button do not move the hero.
        /// </summary>
        public bool PickUp { get; }

        /// <summary>This input with the pick-up button held (or not).</summary>
        public MoveInput WithPickUp(bool held) => new MoveInput(Left, Right, Up, Down, Run, Move, CursorX, CursorY, held);

        public bool Left { get; }
        public bool Right { get; }
        public bool Up { get; }
        public bool Down { get; }
        public bool Run { get; }

        /// <summary>The move button (the right mouse button) is held: the hero heads for the cursor.</summary>
        public bool Move { get; }

        /// <summary>The world cursor's position (the cursor quad's origin).</summary>
        public float CursorX { get; }

        public float CursorY { get; }

        public bool AnyArrow => Left || Right || Up || Down;

        /// <summary>The move button held with the cursor at (x, y), nothing else.</summary>
        public static MoveInput Toward(float x, float y, bool run = false) => new MoveInput(false, false, false, false, run, true, x, y);

        /// <summary>Only the given arrows (and the run key).</summary>
        public static MoveInput Arrows(bool left = false, bool right = false, bool up = false, bool down = false, bool run = false) =>
            new MoveInput(left, right, up, down, run, false, 0f, 0f);

        public bool Equals(MoveInput o) =>
            Left == o.Left && Right == o.Right && Up == o.Up && Down == o.Down && Run == o.Run && Move == o.Move && CursorX.Equals(o.CursorX) && CursorY.Equals(o.CursorY) &&
            PickUp == o.PickUp;

        public override bool Equals(object obj) => obj is MoveInput o && Equals(o);
        public override int GetHashCode() => (Left ? 1 : 0) | (Right ? 2 : 0) | (Up ? 4 : 0) | (Down ? 8 : 0) | (Run ? 16 : 0) | (Move ? 32 : 0) | (PickUp ? 64 : 0) ^ CursorX.GetHashCode() ^ (CursorY.GetHashCode() << 7);
    }

    /// <summary>Sets what a player is holding (<see cref="MoveInput"/>); the party applies it every tick until the next.</summary>
    public sealed class SetMoveInput : ICommand
    {
        public SetMoveInput(PlayerId player, MoveInput input) { Player = player; Input = input; }
        public PlayerId Player { get; }
        public MoveInput Input { get; }
    }

    /// <summary>A hero's AI state or facing changed (it started or stopped walking or running, or turned).</summary>
    public sealed class HeroStateChanged : IGameEvent
    {
        public HeroStateChanged(long tick, int hero, HeroState state, Facing facing)
        {
            Tick = tick;
            Hero = hero;
            State = state;
            Facing = facing;
        }

        public long Tick { get; }
        public int Hero { get; }
        public HeroState State { get; }
        public Facing Facing { get; }
    }

    /// <summary>
    /// Recalls a hero to the party (recallHero, RECONSTRUCTION 4.5): it is placed within the recall spread of the hero
    /// <see cref="ICommand.Player"/> leads, at the recall depth, and falls into place. The party's follow rule recalls a
    /// straggler on its own; this command is the same act, asked for.
    /// </summary>
    public sealed class RecallHero : ICommand
    {
        public RecallHero(PlayerId player, int hero) { Player = player; Hero = hero; }
        public PlayerId Player { get; }

        /// <summary>The hero's place in the party; not the player's own leader.</summary>
        public int Hero { get; }
    }

    /// <summary>A hero was recalled beside its leader at (x, y), depth z, and is falling into place.</summary>
    public sealed class HeroRecalled : IGameEvent
    {
        public HeroRecalled(long tick, int hero, int leader, float x, float y, float z)
        {
            Tick = tick;
            Hero = hero;
            Leader = leader;
            X = x;
            Y = y;
            Z = z;
        }

        public long Tick { get; }
        public int Hero { get; }
        public int Leader { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    /// <summary>
    /// Sets a hero's vitals, as the script's CHANGE HERO HP / MP / AP / STATUS did (assignment; RECONSTRUCTION 9.2): each
    /// given value is clamped to 0 and the hero's maximum. Setting the status Poisoned poisons it until it is set Fine;
    /// Exhausted or Angry give way to the status rule on the next tick, as they did in the original.
    /// </summary>
    public sealed class SetHeroVitals : ICommand
    {
        public SetHeroVitals(PlayerId player, int hero, int? health = null, int? mana = null, int? stamina = null, HeroStatus? status = null)
        {
            Player = player;
            Hero = hero;
            Health = health;
            Mana = mana;
            Stamina = stamina;
            Status = status;
        }

        public PlayerId Player { get; }

        /// <summary>The hero's place in the party.</summary>
        public int Hero { get; }

        public int? Health { get; }
        public int? Mana { get; }
        public int? Stamina { get; }
        public HeroStatus? Status { get; }
    }

    /// <summary>A hero's health, mana or stamina changed (it regenerated, ran, was poisoned or was set).</summary>
    public sealed class HeroVitalsChanged : IGameEvent
    {
        public HeroVitalsChanged(long tick, int hero, int health, int mana, int stamina)
        {
            Tick = tick;
            Hero = hero;
            Health = health;
            Mana = mana;
            Stamina = stamina;
        }

        public long Tick { get; }
        public int Hero { get; }
        public int Health { get; }
        public int Mana { get; }
        public int Stamina { get; }
    }

    /// <summary>A hero's status changed (Exhausted, Fine, Angry or Poisoned).</summary>
    public sealed class HeroStatusChanged : IGameEvent
    {
        public HeroStatusChanged(long tick, int hero, HeroStatus status, HeroStatus previous)
        {
            Tick = tick;
            Hero = hero;
            Status = status;
            Previous = previous;
        }

        public long Tick { get; }
        public int Hero { get; }
        public HeroStatus Status { get; }
        public HeroStatus Previous { get; }
    }

    /// <summary>A falling hero reached the ground (its depth came back to 0).</summary>
    public sealed class HeroLanded : IGameEvent
    {
        public HeroLanded(long tick, int hero)
        {
            Tick = tick;
            Hero = hero;
        }

        public long Tick { get; }
        public int Hero { get; }
    }
}