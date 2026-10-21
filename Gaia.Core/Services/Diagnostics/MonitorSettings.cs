using System;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     What the monitors of a run record and warn about, made once by the host and handed to every monitor it
    ///     attaches.
    /// </summary>
    /// <remarks>
    ///     Nothing here is read from a Brain or Reasoner element, since how closely a run is watched is a property of the
    ///     run rather than of the character.
    /// </remarks>
    public sealed class MonitorSettings
    {
        /// <summary>
        ///     Creates settings recording the given number of cycles of the given number of options each, and warning over
        ///     the given windows.
        /// </summary>
        /// <param name="cycleCapacity">How many cycles each history holds before overwriting its oldest. At least 1.</param>
        /// <param name="optionCapacity">How many options one recorded cycle holds. At least 1.</param>
        /// <param name="doneWarningWindow">How many cycles a selected option may sit done before the warning, or 0 to never warn.</param>
        /// <param name="nothingEligibleForSelectionWarningWindow">
        ///     How many cycles a reasoner may select nothing before the
        ///     warning, or 0.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     cycleCapacity or optionCapacity is below 1, or a warning window is
        ///     negative.
        /// </exception>
        public MonitorSettings(
            int cycleCapacity = 256,
            int optionCapacity = 8,
            int doneWarningWindow = 60,
            int nothingEligibleForSelectionWarningWindow = 60)
        {
            if (cycleCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleCapacity), cycleCapacity,
                    "a history holds at least one cycle");
            }

            if (optionCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(optionCapacity), optionCapacity,
                    "a recorded cycle holds at least one option");
            }

            RefuseNegative(doneWarningWindow, nameof(doneWarningWindow));
            RefuseNegative(nothingEligibleForSelectionWarningWindow, nameof(nothingEligibleForSelectionWarningWindow));

            CycleCapacity = cycleCapacity;
            OptionCapacity = optionCapacity;
            DoneWarningWindow = doneWarningWindow;
            NothingEligibleForSelectionWarningWindow = nothingEligibleForSelectionWarningWindow;
        }

        /// <summary>How many cycles each history holds before overwriting its oldest.</summary>
        public int CycleCapacity { get; }

        /// <summary>How many options one recorded cycle holds.</summary>
        public int OptionCapacity { get; }

        /// <summary>How many cycles a selected option may sit done before the warning, or 0 to never warn.</summary>
        /// <remarks>
        ///     A reasoner that selects by weight does not read an option's IsDone when it selects, so a done option stays
        ///     selected until its weight falls to 0, a veto included, the reasoner selects another option or nothing, or the
        ///     reasoner is disabled. An option that stays selected and done for the whole window usually has a finished veto
        ///     missing. A finished veto is an <see cref="Considerations.ExecutionHistoryConsideration" /> whose
        ///     FinishedWeightFunction reports Vetoed once the option's actions are done. The window is counted in cycles, so on a
        ///     reasoner run less often than once per tick it spans more wall clock.
        /// </remarks>
        public int DoneWarningWindow { get; }

        /// <summary>How many cycles a reasoner may select nothing before the warning, or 0 to never warn.</summary>
        /// <remarks>
        ///     Selecting nothing is legal for a cycle or two, and a reasoner that has run out of work is not counted toward
        ///     the window, but a reasoner with options and no selection for a whole window usually has every option vetoed, and
        ///     the character it decides for stands still.
        /// </remarks>
        public int NothingEligibleForSelectionWarningWindow { get; }

        /// <summary>Refuses a warning window below 0, since 0 already means off.</summary>
        /// <exception cref="ArgumentOutOfRangeException">value is below 0.</exception>
        private static void RefuseNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "a warning window is 0 or a count of cycles");
            }
        }
    }
}
