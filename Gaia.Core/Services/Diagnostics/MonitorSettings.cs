using System;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     What the monitors of a run record and warn about, made once by the host and handed to every monitor it
    ///     attaches.
    /// </summary>
    /// <remarks>
    ///     Settings are immutable once made, so every brain a loader loads and the one brain under investigation cannot
    ///     share a mutable object by accident. They sit on the host's side of the line the configuration language draws. A
    ///     designer's file says what a character does, and how closely a run is watched is a property of the run, so nothing
    ///     here is read from a Brain or Reasoner element. <see cref="Factories.GaiaLoader.Diagnostics" /> applies one to every
    ///     brain a loader loads, and a single load may carry its own.
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
        ///     An option whose actions have finished stays selected until a consideration rules it out, so an option done
        ///     cycle after cycle with nothing ending it usually has a finished veto missing. The window is counted in cycles, so
        ///     on a reasoner run less often than once per tick it spans more wall clock.
        /// </remarks>
        public int DoneWarningWindow { get; }

        /// <summary>How many cycles a reasoner may select nothing before the warning, or 0 to never warn.</summary>
        /// <remarks>
        ///     Selecting nothing is legal for a cycle or two, and a reasoner that has run out of work is excused entirely,
        ///     but a reasoner with options and no selection for a whole window usually has every option vetoed, which plays as a
        ///     character standing still. The two windows are separate settings so either can be turned off without the other.
        /// </remarks>
        public int NothingEligibleForSelectionWarningWindow { get; }

        /// <summary>Refuses a warning window below 0, since 0 already means off.</summary>
        private static void RefuseNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "a warning window is 0 or a count of cycles");
            }
        }
    }
}
