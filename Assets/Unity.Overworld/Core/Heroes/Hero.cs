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
        }

        /// <summary>The hero's place in the party (0 Chrono, 1 Belmont, 2 Flagg).</summary>
        public int Index { get; }

        public HeroDefinition Definition { get; }
        public string Name => Definition.Name;

        /// <summary>Position in world units: the quad's left edge and vertical centre.</summary>
        public float X { get; internal set; }

        public float Y { get; internal set; }
        public float Z { get; internal set; }

        /// <summary>Where the hero was at the start of the last tick, for presentation to interpolate.</summary>
        public float PreviousX { get; internal set; }

        public float PreviousY { get; internal set; }

        /// <summary>Velocity, world units per tick.</summary>
        public float DX { get; internal set; }

        public float DY { get; internal set; }

        public Facing Facing { get; internal set; }
        public HeroState State { get; internal set; }
        public HeroMode Mode { get; internal set; }
        public int Stamina { get; internal set; }

        /// <summary>The run key is held and the hero had stamina (or the mouse sent it running this tick).</summary>
        public bool Running { get; internal set; }

        public bool Kneeling { get; internal set; }
        public bool Laying { get; internal set; }
        public bool Burdened { get; internal set; }

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
        public MoveInput(bool left, bool right, bool up, bool down, bool run, bool move, float cursorX, float cursorY)
        {
            Left = left;
            Right = right;
            Up = up;
            Down = down;
            Run = run;
            Move = move;
            CursorX = cursorX;
            CursorY = cursorY;
        }

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
            Left == o.Left && Right == o.Right && Up == o.Up && Down == o.Down && Run == o.Run && Move == o.Move && CursorX.Equals(o.CursorX) && CursorY.Equals(o.CursorY);

        public override bool Equals(object obj) => obj is MoveInput o && Equals(o);
        public override int GetHashCode() => (Left ? 1 : 0) | (Right ? 2 : 0) | (Up ? 4 : 0) | (Down ? 8 : 0) | (Run ? 16 : 0) | (Move ? 32 : 0) ^ CursorX.GetHashCode() ^ (CursorY.GetHashCode() << 7);
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
}
