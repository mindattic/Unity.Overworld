using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>What a ring menu is doing (aRingMode): at rest, turning left or right, or pulsing out and back in.</summary>
    public enum RingMode
    {
        Select,
        Left,
        Right,
        Out,
        In,
    }

    /// <summary>
    /// One player's battle ring menu and range circles (RingMenu.vb and Circle.vb, RECONSTRUCTION 12.3): whether it is
    /// open, the entry at the top, every entry's angle, the radius (which Up pulses), the circle shown around the leader
    /// (Move's move distance or Attack's weapon range) and its dots. Changed only by <see cref="Party"/>; presentation reads it.
    /// </summary>
    public sealed class RingMenu
    {
        internal RingMenu(BattleTuning tuning)
        {
            Ring = tuning.Ring;
            Circle = tuning.Circle;
            // Room for every icon the G / H developer keys can show (ringIcon(maxRings)); the ring shows its entries.
            angles = new float[Ring.All.Count];
            lastAngles = new float[Ring.All.Count];
            Count = Ring.Entries.Count;
            dotAngles = new float[Circle.Dots];
            dotColours = new DotColour[Circle.Dots];
            // loadcircle: dot n starts at n x the spacing.
            var a = 0f;
            for (var i = 0; i < dotAngles.Length; i++)
            {
                dotAngles[i] = a;
                a += Circle.Spacing;
            }

            Radius = Ring.Radius;
            ResetAngles();
        }

        internal readonly float[] angles, lastAngles, dotAngles;
        internal readonly DotColour[] dotColours;

        public RingMenuTuning Ring { get; }
        public RangeCircleTuning Circle { get; }

        /// <summary>The ring is open (bRingMenu): the arrows turn it, Up pulses it, Enter or a click chooses its top entry.</summary>
        public bool Open { get; internal set; }

        /// <summary>The index of the entry at the top (ringNum).</summary>
        public int Selected { get; internal set; }

        public RingEntry SelectedEntry => Ring.All[Selected].Entry;

        /// <summary>Icon <paramref name="index"/>'s picture (an entry's, or a filler's past them).</summary>
        public string Picture(int index) => Ring.All[index].Picture;

        public RingMode Mode { get; internal set; }

        /// <summary>The ring's radius now (ringRadius): its rest radius, or more while it pulses.</summary>
        public float Radius { get; internal set; }

        /// <summary>Entry <paramref name="index"/>'s angle now, in radians (the top is 1.57).</summary>
        public float Angle(int index) => angles[index];

        /// <summary>
        /// How many icons the ring shows (ringTotal + 1): its entries, or 1 to 8 as the G / H developer keys set it
        /// (<see cref="ResizeRing"/>), the fillers after the entries.
        /// </summary>
        public int Count { get; internal set; }

        /// <summary>The angle between icons and of one turn for <see cref="Count"/> icons (checkRingRotate's deltaAngle).</summary>
        public float SpinStep => Ring.SpinStepFor(Count);

        /// <summary>The move circle is shown (bShowMove): radius the leader's move distance.</summary>
        public bool ShowMove { get; internal set; }

        /// <summary>The attack circle is shown (bShowAttack): radius the leader's weapon's range, every dot red.</summary>
        public bool ShowAttack { get; internal set; }

        /// <summary>A circle is shown around the leader.</summary>
        public bool CircleShown => ShowMove || ShowAttack;

        /// <summary>The circle's radius at the last tick (moveDistance or the weapon's range).</summary>
        public float CircleRadius { get; internal set; }

        /// <summary>
        /// No battle hero of this player's is ready (bNoOneReady): the ring is shut until the first one's gauge is full,
        /// which then leads.
        /// </summary>
        public bool NoOneReady { get; internal set; }

        /// <summary>The button that chose from the ring is still held: it neither kneels to pick up nor uses an item until let go.</summary>
        internal bool ChoiceHeld { get; set; }

        public int DotCount => dotAngles.Length;
        public float DotAngle(int index) => dotAngles[index];
        public DotColour DotColour(int index) => dotColours[index];

        /// <summary>Dot <paramref name="index"/>'s quad origin from the leader's position (add the leader's x and y).</summary>
        public void DotOffset(int index, float heroSize, out float x, out float y)
        {
            var c = Circle;
            var a = dotAngles[index];
            x = heroSize * c.Point.X + c.Offset.X + CircleRadius * (float)Math.Cos(a);
            y = heroSize * c.Point.Y + c.Offset.Y + CircleRadius * (float)Math.Sin(a);
        }

        /// <summary>Entry <paramref name="index"/>'s icon quad origin from the leader's position (renderRingIcon).</summary>
        public void IconOffset(int index, float heroSize, out float x, out float y)
        {
            var r = Ring;
            var a = angles[index];
            x = heroSize * r.IconPoint.X + r.IconOffset.X + Radius * (float)Math.Cos(a);
            y = heroSize * r.IconPoint.Y + r.IconOffset.Y + Radius * (float)Math.Sin(a);
        }

        /// <summary>The select frame's quad origin from the leader's position (renderRingSelect).</summary>
        public void SelectOffset(out float x, out float y)
        {
            x = Ring.SelectOffset.X;
            y = Radius + Ring.SelectOffset.Y;
        }

        /// <summary>resetRing's angles: entry n at the start angle plus n turns, every turn settled.</summary>
        internal void ResetAngles()
        {
            Selected = 0;
            for (var i = 0; i < angles.Length; i++)
            {
                angles[i] = Ring.StartAngle + SpinStep * i;
                lastAngles[i] = angles[i];
            }
        }

        public override string ToString() =>
            (Open ? "open" : "shut") + " at " + SelectedEntry + (Mode != RingMode.Select ? " " + Mode : "") +
            (ShowMove ? ", move circle" : "") + (ShowAttack ? ", attack circle" : "") + (NoOneReady ? ", no one ready" : "");
    }

    /// <summary>
    /// A turn of a player's open ring one entry left (-1: the left arrow, the wheel toward you) or right (+1: the right
    /// arrow, the wheel away), as the held arrows and the wheel do (actionRingLeft / actionRingRight). For scripted runs.
    /// </summary>
    public sealed class TurnRing : ICommand
    {
        public TurnRing(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }
        public int Step { get; }
    }

    /// <summary>
    /// The G (-1) and H (+1) developer keys (frmMain's KeyDown, RECONSTRUCTION 5.2): the ring shows one icon fewer or more
    /// (ringTotal 0 to maxRings 7: 1 to 8 icons, the Coffee Bean, Book and Rune fillers after the five entries) and is
    /// reset (resetRing). Refused unless the developer tools are on.
    /// </summary>
    public sealed class ResizeRing : ICommand
    {
        public ResizeRing(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }
        public int Step { get; }
    }

    /// <summary>A ring now shows <see cref="Count"/> icons (the G / H developer keys).</summary>
    public sealed class RingResized : IGameEvent
    {
        public RingResized(long tick, PlayerId player, int count) { Tick = tick; Player = player; Count = count; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public int Count { get; }
    }

    /// <summary>
    /// Enter or a left click (off a hero and the buttons) with the ring open (actionRingSelect): the entry at the top is
    /// chosen. Move (with stamina left) lets the leader walk its move distance; Item (with a pack not empty) opens the
    /// pack; End passes the turn; Attack and Skill do nothing (the original gave them no handler).
    /// </summary>
    public sealed class ChooseFromRing : ICommand
    {
        public ChooseFromRing(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>An entry was chosen from a ring; <see cref="Taken"/> false when it did nothing (no stamina, an empty pack, Attack, Skill).</summary>
    public sealed class RingEntryChosen : IGameEvent
    {
        public RingEntryChosen(long tick, PlayerId player, int hero, RingEntry entry, bool taken)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            Entry = entry;
            Taken = taken;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public int Hero { get; }
        public RingEntry Entry { get; }
        public bool Taken { get; }
    }

    /// <summary>A ring finished a turn: <see cref="Entry"/> is at the top now.</summary>
    public sealed class RingTurned : IGameEvent
    {
        public RingTurned(long tick, PlayerId player, RingEntry entry) { Tick = tick; Player = player; Entry = entry; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public RingEntry Entry { get; }
    }

    /// <summary>
    /// The battle turn passed from hero <see cref="From"/> (now recharging) to <see cref="To"/>, or to no one
    /// (<see cref="To"/> -1: no battle hero is ready; the first whose gauge fills will lead).
    /// </summary>
    public sealed class TurnPassed : IGameEvent
    {
        public TurnPassed(long tick, PlayerId player, int from, int to) { Tick = tick; Player = player; From = from; To = to; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public int From { get; }
        public int To { get; }
    }
}
