namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// How heroes rest and the party sleeps (Input.actionRest, Heroes.aiKneeling's chooser, Sun.checkSleep; RECONSTRUCTION
    /// 6.4), heroes.json "rest".
    /// </summary>
    public sealed class RestTuning
    {
        /// <summary>The fewest hours a sleep can be set to, and what the chooser returns to after a sleep (1).</summary>
        public int MinHours { get; internal set; }

        /// <summary>The most hours a sleep can be set to (24).</summary>
        public int MaxHours { get; internal set; }

        /// <summary>While laid, a held arrow changes the hours once this many seconds have passed since the last check (0.08).</summary>
        public float ChooserRate { get; internal set; }

        /// <summary>The share of each maximum every hour of sleep restores (0.125, one-eighth).</summary>
        public float RestorePerHour { get; internal set; }

        /// <summary>How much each ambient channel falls (to black) and rises (back to white) a tick (fadeSPD 5).</summary>
        public int FadeStep { get; internal set; }

        internal static RestTuning Read(ContentReader r)
        {
            var t = new RestTuning
            {
                MinHours = r.Integer("minHours", 1, 1000),
                MaxHours = r.Integer("maxHours", 1, 1000),
                ChooserRate = r.Positive("chooserRate"),
                RestorePerHour = r.Number("restorePerHour", 0f, 1f),
                FadeStep = r.Integer("fadeStep", 1, 255),
            };
            r.Check(t.MaxHours >= t.MinHours, "maxHours", "is below minHours");
            r.Done();
            return t;
        }
    }
}
