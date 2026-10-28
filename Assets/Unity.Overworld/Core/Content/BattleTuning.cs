using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// How a hero's turn gauge refills in battle mode (Heroes.checkRegen's battle branch, refillInterval; RECONSTRUCTION 7
    /// and 12.1), heroes.json "battle". Battle has no initiative: the stamina that powers running is the turn gauge, and a
    /// hero Recharging kneels while it refills. Also the ring menu and its range circles (RECONSTRUCTION 12.3).
    /// </summary>
    public sealed class BattleTuning
    {
        /// <summary>A refill check comes once more than this many seconds have passed since the last (0.18).</summary>
        public float RefillInterval { get; internal set; }

        /// <summary>The stamina a Recharging hero below its maximum gains at each check (1).</summary>
        public int RefillStamina { get; internal set; }

        /// <summary>The share of its maximum stamina a use of an item costs the leader in battle (checkUseItem, 0.1).</summary>
        public float ItemStamina { get; internal set; }

        /// <summary>The ring menu around the leader (RingMenu.vb).</summary>
        public RingMenuTuning Ring { get; internal set; }

        /// <summary>The dotted range circles (Circle.vb).</summary>
        public RangeCircleTuning Circle { get; internal set; }

        internal static BattleTuning Read(ContentReader r)
        {
            var t = new BattleTuning
            {
                RefillInterval = r.Positive("refillInterval"),
                RefillStamina = r.Integer("refillStamina", 1),
                ItemStamina = r.Number("itemStamina", 0f, 1f),
                Ring = RingMenuTuning.Read(r.Section("ring")),
                Circle = RangeCircleTuning.Read(r.Section("circle")),
            };
            r.Done();
            return t;
        }
    }

    /// <summary>
    /// The ring menu's entries (ringMove 0, ringEnd 1, ringSkill 2, ringItem 3, ringAttack 4), and the fillers the G / H
    /// developer keys add after them (ringIcon 5 to 7: the Coffee Bean, Book and Rune icons), which do nothing.
    /// </summary>
    public enum RingEntry
    {
        Move,
        End,
        Skill,
        Item,
        Attack,
        Filler,
    }

    /// <summary>One entry of the ring: what it is and its icon.</summary>
    public sealed class RingEntryDefinition
    {
        public RingEntry Entry { get; internal set; }
        public string Picture { get; internal set; }
    }

    /// <summary>
    /// The ring menu (RingMenu.vb, RECONSTRUCTION 12.3), heroes.json "battle.ring": its entries in ring order, their icons'
    /// size and place around the leader, the select frame, the spin and the pulse.
    /// </summary>
    public sealed class RingMenuTuning
    {
        public IReadOnlyList<RingEntryDefinition> Entries { get; internal set; }

        /// <summary>
        /// The filler icons after the entries (ringIcon 5, 6 and 7: coffee_bean, book2, rune1), shown only when the G / H
        /// developer keys grow the ring past its entries (maxRings 7: at most eight icons).
        /// </summary>
        public IReadOnlyList<RingEntryDefinition> Fillers { get; internal set; }

        /// <summary>The entries then the fillers: every icon the ring can show, in ring order.</summary>
        public IReadOnlyList<RingEntryDefinition> All { get; internal set; }

        /// <summary>The ring's radius at rest (defaultRadius, 0.1).</summary>
        public float Radius { get; internal set; }

        /// <summary>An icon's quad: this wide and half as high above and below its centre line (RingIconSize, 0.05).</summary>
        public float IconSize { get; internal set; }

        /// <summary>The ring's centre from the leader's position: heroSize times this, plus <see cref="IconOffset"/>.</summary>
        public DataPoint IconPoint { get; internal set; }

        public DataPoint IconOffset { get; internal set; }
        public string SelectPicture { get; internal set; }

        /// <summary>The select frame's quad (ringSelectSize, 0.075).</summary>
        public float SelectSize { get; internal set; }

        /// <summary>The select frame from the leader's position (plus the radius on y).</summary>
        public DataPoint SelectOffset { get; internal set; }

        /// <summary>The first entry's angle, at the top (1.57).</summary>
        public float StartAngle { get; internal set; }

        /// <summary>The angle between entries and of one turn with the ring's own entries (deltaAngle, 1.256 for five).</summary>
        public float SpinStep => SpinStepFor(Entries.Count);

        /// <summary>
        /// checkRingRotate's deltaAngle for one to eight icons (6.28, 3.14, 2.093, 1.57, 1.256, 1.046, 0.897, 0.785):
        /// resetRing placed icon n at the start angle plus n of these, and a turn moves every icon one of them.
        /// </summary>
        public IReadOnlyList<float> SpinSteps { get; internal set; }

        /// <summary>The angle between icons, and of one turn, with <paramref name="count"/> icons shown.</summary>
        public float SpinStepFor(int count) => SpinSteps[Math.Max(1, Math.Min(count, SpinSteps.Count)) - 1];

        /// <summary>A turn eases each angle by SpinBase + SpinEase x what is left, every tick (0.1, ringSPD 0.3).</summary>
        public float SpinBase { get; internal set; }

        public float SpinEase { get; internal set; }

        /// <summary>Up pulses the radius out to this (1) by <see cref="PulseStep"/> a tick, then back in.</summary>
        public float PulseRadius { get; internal set; }

        public float PulseStep { get; internal set; }

        /// <summary>The index of <paramref name="entry"/> in the ring, or -1.</summary>
        public int IndexOf(RingEntry entry)
        {
            for (var i = 0; i < Entries.Count; i++)
                if (Entries[i].Entry == entry) return i;
            return -1;
        }

        internal static RingMenuTuning Read(ContentReader r)
        {
            var entries = new List<RingEntryDefinition>();
            foreach (var e in r.Objects("entries"))
            {
                entries.Add(new RingEntryDefinition { Entry = e.Choice<RingEntry>("entry"), Picture = e.Text("picture") });
                e.Done();
            }

            var fillers = new List<RingEntryDefinition>();
            foreach (var picture in r.Texts("fillers")) fillers.Add(new RingEntryDefinition { Entry = RingEntry.Filler, Picture = picture });
            var all = new List<RingEntryDefinition>(entries);
            all.AddRange(fillers);

            var steps = new List<float>();
            var raw = r.Raw("spinSteps", JsonKind.Array, "an array of numbers");
            if (raw != null)
                foreach (var item in raw.Items)
                {
                    if (item.Kind != JsonKind.Number || !(item.Number > 0))
                    {
                        r.Fail("spinSteps", "must hold only numbers greater than 0");
                        continue;
                    }

                    steps.Add((float)item.Number);
                }

            var t = new RingMenuTuning
            {
                Entries = entries,
                Fillers = fillers,
                All = all,
                SpinSteps = steps,
                Radius = r.Positive("radius"),
                IconSize = r.Positive("iconSize"),
                IconPoint = r.Pair("iconPoint"),
                IconOffset = r.Pair("iconOffset"),
                SelectPicture = r.Text("selectPicture"),
                SelectSize = r.Positive("selectSize"),
                SelectOffset = r.Pair("selectOffset"),
                StartAngle = r.Number("startAngle"),
                SpinBase = r.Positive("spinBase"),
                SpinEase = r.Number("spinEase", 0f, 1f),
                PulseRadius = r.Positive("pulseRadius"),
                PulseStep = r.Positive("pulseStep"),
            };
            r.Check(entries.Count > 0, "entries", "must list at least one entry");
            r.Check(steps.Count == all.Count, "spinSteps", "must give one step for each count of icons, 1 to the entries and fillers (" + all.Count + "; gives " + steps.Count + ")");
            foreach (RingEntry e in System.Enum.GetValues(typeof(RingEntry)))
            {
                if (e == RingEntry.Filler) continue;
                var count = 0;
                foreach (var d in entries) if (d.Entry == e) count++;
                r.Check(count == 1, "entries", "must list " + e + " exactly once (lists it " + count + " times)");
            }

            r.Done();
            return t;
        }
    }

    /// <summary>The colour of a range circle's dot (checkDotColor).</summary>
    public enum DotColour
    {
        Green,
        Blue,
        Red,
    }

    /// <summary>
    /// The dotted range circles (Circle.vb, RECONSTRUCTION 12.3), heroes.json "battle.circle": how many dots, their size,
    /// spacing and orbit, where they sit around the leader, and the colour rule.
    /// </summary>
    public sealed class RangeCircleTuning
    {
        public int Dots { get; internal set; }
        public float DotSize { get; internal set; }

        /// <summary>Dot n starts at n x this angle (0.3).</summary>
        public float Spacing { get; internal set; }

        /// <summary>The radians each dot orbits a tick (0.01).</summary>
        public float Orbit { get; internal set; }

        public DataPoint Point { get; internal set; }
        public DataPoint Offset { get; internal set; }
        public DataPoint NearPoint { get; internal set; }
        public DataPoint NearOffset { get; internal set; }
        public float NearRange { get; internal set; }
        public string GreenPicture { get; internal set; }
        public string BluePicture { get; internal set; }
        public string RedPicture { get; internal set; }

        public string Picture(DotColour colour) => colour == DotColour.Red ? RedPicture : colour == DotColour.Blue ? BluePicture : GreenPicture;

        internal static RangeCircleTuning Read(ContentReader r)
        {
            var t = new RangeCircleTuning
            {
                Dots = r.Integer("dots", 1),
                DotSize = r.Positive("dotSize"),
                Spacing = r.Number("spacing"),
                Orbit = r.Number("orbit"),
                Point = r.Pair("point"),
                Offset = r.Pair("offset"),
                NearPoint = r.Pair("nearPoint"),
                NearOffset = r.Pair("nearOffset"),
                NearRange = r.Positive("nearRange"),
                GreenPicture = r.Text("greenPicture"),
                BluePicture = r.Text("bluePicture"),
                RedPicture = r.Text("redPicture"),
            };
            r.Done();
            return t;
        }
    }
}
