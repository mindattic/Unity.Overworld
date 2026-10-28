using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The render switches F1, F2, F3 and F5 flip (frmMain's KeyDown).</summary>
    public enum RenderSwitch
    {
        /// <summary>F1: wireframe (FillMode.WireFrame); off at start.</summary>
        Wireframe,

        /// <summary>F2: the pictures (useTextures); on at start. Off, every world quad is drawn untextured.</summary>
        Textures,

        /// <summary>F3: back-face culling (Cull.CounterClockwise); off at start.</summary>
        BackFaceCulling,

        /// <summary>F5: the world's texture filter, Linear (on, as at start) or Point (off).</summary>
        LinearFilter,
    }

    /// <summary>What the path dots do (Path.aPathMode), in the order P steps them: off, show, set (laying), show2.</summary>
    public enum PathMode
    {
        Off,
        Show,
        Set,
        Show2,
    }

    /// <summary>F1, F2, F3 or F5: <see cref="Switch"/> flips. Refused unless the developer tools are on.</summary>
    public sealed class ToggleRenderSwitch : ICommand
    {
        public ToggleRenderSwitch(PlayerId player, RenderSwitch which) { Player = player; Switch = which; }
        public PlayerId Player { get; }
        public RenderSwitch Switch { get; }
    }

    /// <summary>
    /// - (<see cref="Step"/> -1) or = (+1) held this frame: the game speed falls (not below the floor) or rises by the
    /// step. Refused unless the developer tools are on.
    /// </summary>
    public sealed class ChangeGameSpeed : ICommand
    {
        public ChangeGameSpeed(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }
        public int Step { get; }
    }

    /// <summary>L: the test message is queued. Refused unless the developer tools are on.</summary>
    public sealed class ShowTestMessage : ICommand
    {
        public ShowTestMessage(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>P: the path dots step off, show, set, show2, off; the player's leader lays them. Refused unless the developer tools are on.</summary>
    public sealed class CyclePathMode : ICommand
    {
        public CyclePathMode(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>O: the last dots are cleared (clearpath(20)). Refused unless the developer tools are on.</summary>
    public sealed class ClearPath : ICommand
    {
        public ClearPath(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    public sealed class RenderSwitchChanged : IGameEvent
    {
        public RenderSwitchChanged(long tick, PlayerId player, RenderSwitch which, bool on) { Tick = tick; Player = player; Switch = which; On = on; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public RenderSwitch Switch { get; }
        public bool On { get; }
    }

    public sealed class GameSpeedChanged : IGameEvent
    {
        public GameSpeedChanged(long tick, PlayerId player, float speed) { Tick = tick; Player = player; Speed = speed; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public float Speed { get; }
    }

    public sealed class PathModeChanged : IGameEvent
    {
        public PathModeChanged(long tick, PlayerId player, PathMode mode) { Tick = tick; Player = player; Mode = mode; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public PathMode Mode { get; }
    }

    public sealed class PathCleared : IGameEvent
    {
        public PathCleared(long tick, PlayerId player, int next) { Tick = tick; Player = player; Next = next; }
        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The slot the next dot goes in (pathNum) after the clear.</summary>
        public int Next { get; }
    }

    /// <summary>
    /// The path dots (Path.vb, RECONSTRUCTION 14.3): the slots, which are laid, and where the next goes (pathNum). Changed
    /// only by <see cref="DeveloperTools"/>; presentation reads it.
    /// </summary>
    public sealed class PathTrail
    {
        readonly float[] x, y;
        readonly bool[] active;

        internal PathTrail(PathTuning tuning)
        {
            Tuning = tuning;
            x = new float[tuning.Slots];
            y = new float[tuning.Slots];
            active = new bool[tuning.Slots];
            x[0] = tuning.Start.X;
            y[0] = tuning.Start.Y;
            Next = 1;
        }

        public PathTuning Tuning { get; }

        /// <summary>The mode (pathMode): dots are laid in Set and drawn in every mode but Off.</summary>
        public PathMode Mode { get; internal set; }

        /// <summary>The player whose leader lays the dots (the one who last pressed P).</summary>
        public PlayerId Player { get; internal set; } = PlayerId.One;

        /// <summary>The slot the next dot goes in (pathNum, from 1).</summary>
        public int Next { get; private set; }

        public int Slots => active.Length;
        public bool Active(int slot) => active[slot];
        public float X(int slot) => x[slot];
        public float Y(int slot) => y[slot];

        /// <summary>How many slots are laid now.</summary>
        public int Laid
        {
            get
            {
                var n = 0;
                foreach (var a in active) if (a) n++;
                return n;
            }
        }

        /// <summary>The dots are drawn (every mode but Off).</summary>
        public bool Shown => Mode != PathMode.Off;

        /// <summary>
        /// checkPathPoints, in Set mode: facing left or right, a dot is laid once the leader's centre x (x + heroSize / 2) is
        /// not within the range of the last dot's; facing up or down, its centre y (y - heroSize / 2). The dot goes at the
        /// centre, lifted, and the next slot is taken (the last slot is laid again once every one is used). True when laid.
        /// </summary>
        internal bool Lay(Hero hero, float heroSize)
        {
            if (Mode != PathMode.Set) return false;
            var cx = hero.X + heroSize / 2f;
            var cy = hero.Y - heroSize / 2f;
            var last = Next - 1;
            var far = hero.Facing == Facing.Left || hero.Facing == Facing.Right
                ? !WorldCamera.Near(cx, x[last], Tuning.Range)
                : !WorldCamera.Near(cy, y[last], Tuning.Range);
            if (!far) return false;
            active[Next] = true;
            x[Next] = cx;
            y[Next] = cy + Tuning.Lift;
            if (Next < Slots - 1) Next++;
            return true;
        }

        /// <summary>
        /// clearpath: with at least <paramref name="count"/> dots past slot 0, slots Next - count to Next are cleared and
        /// Next goes back by count; otherwise every slot up to Next is cleared and Next is 1 again.
        /// </summary>
        internal void Clear(int count)
        {
            if (Next - count >= 1)
            {
                for (var c = Next - count; c <= Next && c < Slots; c++) active[c] = false;
                Next -= count;
            }
            else
            {
                for (var c = 0; c <= Next && c < Slots; c++) active[c] = false;
                Next = 1;
            }
        }
    }

    /// <summary>
    /// The developer tools (U.OVW-T47; RECONSTRUCTION 5.1, 5.2 and 14.3; BIBLE "Debug keys"), behind one switch
    /// (developer.json "enabled", off as shipped, or <c>-overworld-developer</c>): every command here is refused while it is
    /// off. F1, F2, F3 and F5 flip the render switches the presentation applies (<see cref="RenderSwitch"/>); G and H
    /// grow and shrink the battle ring (<see cref="ResizeRing"/>, through the party); L queues the test message; - and =
    /// change the game speed the heroes move and the clock runs by (<see cref="ChangeGameSpeed"/>); P and O drive the
    /// path dots (<see cref="Trail"/>), laid every tick in Set mode, paused or not (checkPathPoints ran in the draw). The
    /// U key's <see cref="ToggleHeroMode"/> is the party's own command, sent only while the tools are on.
    /// </summary>
    public sealed class DeveloperTools : ITickSystem
    {
        readonly Party party;
        readonly WorldClock clock;
        readonly MessageBoard messages;

        public DeveloperTools(DeveloperData data, Party party = null, WorldClock clock = null, MessageBoard messages = null, bool? enabled = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            this.party = party;
            this.clock = clock;
            this.messages = messages;
            Enabled = enabled ?? data.Enabled;
            Trail = new PathTrail(data.Path);
        }

        public DeveloperData Data { get; }

        /// <summary>The tools are on: the keys answer and the wall lines are drawn.</summary>
        public bool Enabled { get; }

        public PathTrail Trail { get; }

        public bool Wireframe { get; private set; }
        public bool Textures { get; private set; } = true;
        public bool BackFaceCulling { get; private set; }

        /// <summary>The world's filter is Linear (worldFilter's start), or Point once F5 is pressed.</summary>
        public bool LinearFilter { get; private set; } = true;

        /// <summary>The game speed now (the party's; the clock follows it).</summary>
        public float GameSpeed => party != null ? party.GameSpeed : clock?.GameSpeed ?? 1f;

        public bool Switch(RenderSwitch which)
        {
            switch (which)
            {
                case RenderSwitch.Wireframe: return Wireframe;
                case RenderSwitch.Textures: return Textures;
                case RenderSwitch.BackFaceCulling: return BackFaceCulling;
                default: return LinearFilter;
            }
        }

        public DeveloperTools Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<ToggleRenderSwitch>(HandleSwitch);
            game.Register<ChangeGameSpeed>(HandleSpeed);
            game.Register<ShowTestMessage>(HandleMessage);
            game.Register<CyclePathMode>(HandlePathMode);
            game.Register<ClearPath>(HandleClearPath);
            game.Register<ResizeRing>(HandleResizeRing);
            game.AddSystem(this);
            return this;
        }

        static CommandResult Off() => CommandResult.Reject("The developer tools are off.");

        CommandResult HandleSwitch(Game game, ToggleRenderSwitch command)
        {
            if (!Enabled) return Off();
            bool on;
            switch (command.Switch)
            {
                case RenderSwitch.Wireframe: on = Wireframe = !Wireframe; break;
                case RenderSwitch.Textures: on = Textures = !Textures; break;
                case RenderSwitch.BackFaceCulling: on = BackFaceCulling = !BackFaceCulling; break;
                case RenderSwitch.LinearFilter: on = LinearFilter = !LinearFilter; break;
                default: return CommandResult.Reject("No such switch.");
            }

            return CommandResult.Accept(new RenderSwitchChanged(game.Tick, command.Player, command.Switch, on));
        }

        CommandResult HandleSpeed(Game game, ChangeGameSpeed command)
        {
            if (!Enabled) return Off();
            if (command.Step != 1 && command.Step != -1) return CommandResult.Reject("Change the game speed down (-1) or up (+1).");
            // gameSpeed -= 0.01 : If gameSpeed < 0 Then gameSpeed = 0; gameSpeed += 0.01.
            var speed = GameSpeed + command.Step * Data.GameSpeedStep;
            if (speed < Data.GameSpeedFloor) speed = Data.GameSpeedFloor;
            if (party != null) party.GameSpeed = speed;
            if (clock != null) clock.GameSpeed = speed;
            return CommandResult.Accept(new GameSpeedChanged(game.Tick, command.Player, speed));
        }

        CommandResult HandleMessage(Game game, ShowTestMessage command)
        {
            if (!Enabled) return Off();
            if (messages == null) return CommandResult.Reject("There is no message board.");
            var c = Data.TestColour;
            messages.Add(Data.TestText, Data.TestFont, c.R, c.G, c.B);
            return CommandResult.Accept();
        }

        CommandResult HandlePathMode(Game game, CyclePathMode command)
        {
            if (!Enabled) return Off();
            var t = Trail;
            t.Mode = t.Mode == PathMode.Off ? PathMode.Show : t.Mode == PathMode.Show ? PathMode.Set : t.Mode == PathMode.Set ? PathMode.Show2 : PathMode.Off;
            t.Player = command.Player;
            return CommandResult.Accept(new PathModeChanged(game.Tick, command.Player, t.Mode));
        }

        CommandResult HandleClearPath(Game game, ClearPath command)
        {
            if (!Enabled) return Off();
            Trail.Clear(Data.Path.Clear);
            return CommandResult.Accept(new PathCleared(game.Tick, command.Player, Trail.Next));
        }

        CommandResult HandleResizeRing(Game game, ResizeRing command)
        {
            if (!Enabled) return Off();
            if (party == null) return CommandResult.Reject("There is no party.");
            if (command.Step != 1 && command.Step != -1) return CommandResult.Reject("Shrink the ring (-1) or grow it (+1).");
            var events = new List<IGameEvent>();
            party.ResizeRing(command.Player, command.Step, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>checkPathPoints, every tick (renderPath ran in the draw, so a pause does not stop it).</summary>
        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            if (!Enabled || party == null || Trail.Mode != PathMode.Set || !game.HasPlayer(Trail.Player)) return;
            Trail.Lay(party.Leader(Trail.Player), party.Data.HeroSize);
        }
    }
}
