using Gaia.Core.Strings;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     One recorded cycle's bookkeeping, the roll that decided it, the option selected, and how many options it
    ///     holds.
    /// </summary>
    public readonly struct DecisionCycle
    {
        internal DecisionCycle(long cycleIndex, float roll, AiString selectedName, int recordCount)
        {
            CycleIndex = cycleIndex;
            Roll = roll;
            SelectedName = selectedName;
            RecordCount = recordCount;
        }

        /// <summary>
        ///     Which cycle this was. A reasoner monitored through its brain records the brain's cycle, so every history in
        ///     the brain counts alike, and a monitor attached by hand counts the cycles it has seen.
        /// </summary>
        public long CycleIndex { get; }

        /// <summary>
        ///     The roll that decided the cycle, in [0, 1) before the reasoner scaled it by the options' total weight, or 0 on
        ///     a cycle with nothing rolled.
        /// </summary>
        public float Roll { get; }

        /// <summary>The name of the option selected, or <see cref="AiString.Empty" /> when nothing was selected.</summary>
        public AiString SelectedName { get; }

        /// <summary>How many option records the cycle holds.</summary>
        public int RecordCount { get; }
    }
}
