namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// How a hero's turn gauge refills in battle mode (Heroes.checkRegen's battle branch, refillInterval; RECONSTRUCTION 7
    /// and 12.1), heroes.json "battle". Battle has no initiative: the stamina that powers running is the turn gauge, and a
    /// hero Recharging kneels while it refills.
    /// </summary>
    public sealed class BattleTuning
    {
        /// <summary>A refill check comes once more than this many seconds have passed since the last (0.18).</summary>
        public float RefillInterval { get; internal set; }

        /// <summary>The stamina a Recharging hero below its maximum gains at each check (1).</summary>
        public int RefillStamina { get; internal set; }

        internal static BattleTuning Read(ContentReader r)
        {
            var t = new BattleTuning
            {
                RefillInterval = r.Positive("refillInterval"),
                RefillStamina = r.Integer("refillStamina", 1),
            };
            r.Done();
            return t;
        }
    }
}
