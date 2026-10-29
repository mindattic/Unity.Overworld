namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// How the followers keep up with the leader (Heroes.checkfollow; RECONSTRUCTION 4.5), heroes.json "follow". Every
    /// range is tested per axis, as the original's A_near_B was: a follower is within a range when both axes are.
    /// </summary>
    public sealed class FollowTuning
    {
        /// <summary>Following is on when the game starts (bTeam, True).</summary>
        public bool TeamAtStart { get; internal set; }

        /// <summary>Beyond this on either axis the follower is recalled beside the leader (1).</summary>
        public float RecallRange { get; internal set; }

        /// <summary>Within the recall range the follower runs, its velocity this times the distance (0.03).</summary>
        public float RunSpeed { get; internal set; }

        /// <summary>Within this on both axes it walks instead (0.2).</summary>
        public float WalkRange { get; internal set; }

        /// <summary>Walking, its velocity is this times the distance (0.02).</summary>
        public float WalkSpeed { get; internal set; }

        /// <summary>Within this on both axes it stands (0.05).</summary>
        public float StandRange { get; internal set; }

        internal static FollowTuning Read(ContentReader r)
        {
            var t = new FollowTuning
            {
                TeamAtStart = r.Flag("teamAtStart"),
                RecallRange = r.Positive("recallRange"),
                RunSpeed = r.Positive("runSpeed"),
                WalkRange = r.Positive("walkRange"),
                WalkSpeed = r.Positive("walkSpeed"),
                StandRange = r.Positive("standRange"),
            };
            r.Check(t.WalkRange < t.RecallRange, "walkRange", "must be below recallRange");
            r.Check(t.StandRange < t.WalkRange, "standRange", "must be below walkRange");
            r.Done();
            return t;
        }
    }

    /// <summary>
    /// How two crowding followers slide apart (Heroes.checkFlocking; RECONSTRUCTION 4.5), heroes.json "flocking".
    /// </summary>
    public sealed class FlockingTuning
    {
        /// <summary>Two moving followers within this on both axes crowd each other (0.05).</summary>
        public float Range { get; internal set; }

        /// <summary>Each is nudged sideways by its velocity across the other axis divided by this (6).</summary>
        public float Divisor { get; internal set; }

        internal static FlockingTuning Read(ContentReader r)
        {
            var t = new FlockingTuning
            {
                Range = r.Positive("range"),
                Divisor = r.Positive("divisor"),
            };
            r.Done();
            return t;
        }
    }

    /// <summary>
    /// Which hero the world cursor is over (Cursor.checkCursorOverHero; RECONSTRUCTION 4.7), heroes.json "selection":
    /// the hero's point (heroSize times <see cref="HeroPoint"/> plus <see cref="HeroOffset"/> from its position) within
    /// <see cref="Range"/> on both axes of the cursor's point (its position plus <see cref="CursorOffset"/>).
    /// </summary>
    public sealed class SelectionTuning
    {
        /// <summary>The hero's point as fractions of heroSize (1/2 across, 1/2 down).</summary>
        public DataPoint HeroPoint { get; internal set; }

        /// <summary>Added to the hero's point (0, +0.03).</summary>
        public DataPoint HeroOffset { get; internal set; }

        /// <summary>The cursor's point from its position (+0.03, -0.05).</summary>
        public DataPoint CursorOffset { get; internal set; }

        /// <summary>How near on each axis the two points must be (0.03).</summary>
        public float Range { get; internal set; }

        internal static SelectionTuning Read(ContentReader r)
        {
            var t = new SelectionTuning
            {
                HeroPoint = r.Pair("heroPoint", -1f, 1f),
                HeroOffset = r.Pair("heroOffset"),
                CursorOffset = r.Pair("cursorOffset"),
                Range = r.Positive("range"),
            };
            r.Done();
            return t;
        }
    }
}
