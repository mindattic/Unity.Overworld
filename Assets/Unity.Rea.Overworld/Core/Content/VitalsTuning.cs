using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A hero's status (the original's heroStatus): shown as an animation above it (StatusIcon.vb).</summary>
    public enum HeroStatus
    {
        Exhausted,
        Fine,
        Angry,
        Poisoned,
    }

    /// <summary>
    /// How the heroes' vitals change and show (Heroes.checkStatus, checkRegen and Gui/StatusIcon.vb; RECONSTRUCTION 7),
    /// heroes.json "vitals". The regeneration step comes every hero's regenRate (2 s) in adventure mode.
    /// </summary>
    public sealed class VitalsTuning
    {
        /// <summary>Not poisoned, stamina below this fraction of its maximum: Exhausted (0.15).</summary>
        public float ExhaustedBelow { get; internal set; }

        /// <summary>Not poisoned, health below this fraction of its maximum: Angry (0.05), over Exhausted.</summary>
        public float AngryBelow { get; internal set; }

        /// <summary>Stamina a standing hero gains each step (+1).</summary>
        public int StandingStamina { get; internal set; }

        /// <summary>Stamina a kneeling hero (not picking up) gains each step (+2).</summary>
        public int KneelingStamina { get; internal set; }

        /// <summary>Stamina a running hero loses each step (1), poisoned or not.</summary>
        public int RunningStamina { get; internal set; }

        /// <summary>Health a poisoned hero loses each step (1); a poisoned hero gains no stamina.</summary>
        public int PoisonHealth { get; internal set; }

        /// <summary>The status icon's square side (iconSize 0.05), its quad centred vertically on its point.</summary>
        public float IconSize { get; internal set; }

        /// <summary>The icon's point from the hero's position (+0.045, +0.03).</summary>
        public DataPoint IconOffset { get; internal set; }

        /// <summary>Seconds per icon frame (0.3).</summary>
        public float IconRate { get; internal set; }

        public IReadOnlyList<string> ExhaustedIcon { get; internal set; }
        public IReadOnlyList<string> AngryIcon { get; internal set; }

        /// <summary>The poisoned animation's frames (the Angry frames: the original's Poisoned folder is empty).</summary>
        public IReadOnlyList<string> PoisonedIcon { get; internal set; }

        /// <summary>The colour the poisoned frames are drawn in, so poison reads apart from anger (BIBLE).</summary>
        public LightColour PoisonedTint { get; internal set; }

        /// <summary>The frames of <paramref name="status"/>'s animation, or none for Fine (the original drew a blank).</summary>
        public IReadOnlyList<string> Icon(HeroStatus status)
        {
            switch (status)
            {
                case HeroStatus.Exhausted: return ExhaustedIcon;
                case HeroStatus.Angry: return AngryIcon;
                case HeroStatus.Poisoned: return PoisonedIcon;
                default: return System.Array.Empty<string>();
            }
        }

        /// <summary>Every icon picture, once each.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                foreach (var f in ExhaustedIcon) if (!list.Contains(f)) list.Add(f);
                foreach (var f in AngryIcon) if (!list.Contains(f)) list.Add(f);
                foreach (var f in PoisonedIcon) if (!list.Contains(f)) list.Add(f);
                return list;
            }
        }

        internal static VitalsTuning Read(ContentReader r)
        {
            var t = new VitalsTuning
            {
                ExhaustedBelow = r.Number("exhaustedBelow", 0f, 1f),
                AngryBelow = r.Number("angryBelow", 0f, 1f),
                StandingStamina = r.Integer("standingStamina", 0, 1000),
                KneelingStamina = r.Integer("kneelingStamina", 0, 1000),
                RunningStamina = r.Integer("runningStamina", 0, 1000),
                PoisonHealth = r.Integer("poisonHealth", 0, 1000),
                IconSize = r.Positive("iconSize"),
                IconOffset = r.Pair("iconOffset"),
                IconRate = r.Positive("iconRate"),
                ExhaustedIcon = Frames(r, "exhaustedIcon"),
                AngryIcon = Frames(r, "angryIcon"),
                PoisonedIcon = Frames(r, "poisonedIcon"),
                PoisonedTint = TimeData.ReadColour(r, "poisonedTint"),
            };
            r.Done();
            return t;
        }

        static IReadOnlyList<string> Frames(ContentReader r, string key)
        {
            var frames = r.Texts(key);
            r.Check(frames.Count > 0, key, "must name at least one picture");
            foreach (var f in frames) r.Check(f.StartsWith("gfx/", System.StringComparison.Ordinal), key, "must hold paths under gfx/ (has \"" + f + "\")");
            return frames;
        }
    }
}
