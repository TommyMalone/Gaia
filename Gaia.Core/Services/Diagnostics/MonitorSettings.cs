using System;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>What the monitors of a run record, made once by the host and handed to every monitor it attaches.</summary>
    /// <remarks>
    ///     Settings are immutable once made, so every brain a loader loads and the one brain under investigation cannot
    ///     share a mutable object by accident. They sit on the host's side of the line the configuration language draws. A
    ///     designer's file says what a character does, and how closely a run is watched is a property of the run, so nothing
    ///     here is read from a Brain or Reasoner element. <see cref="Factories.GaiaLoader.Diagnostics" /> applies one to every
    ///     brain a loader loads, and a single load may carry its own.
    /// </remarks>
    public sealed class MonitorSettings
    {
        /// <summary>Creates settings recording the given number of cycles of the given number of options each.</summary>
        /// <param name="cycleCapacity">How many cycles each history holds before overwriting its oldest. At least 1.</param>
        /// <param name="optionCapacity">How many options one recorded cycle holds. At least 1.</param>
        public MonitorSettings(int cycleCapacity = 256, int optionCapacity = 8)
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

            CycleCapacity = cycleCapacity;
            OptionCapacity = optionCapacity;
        }

        /// <summary>How many cycles each history holds before overwriting its oldest.</summary>
        public int CycleCapacity { get; }

        /// <summary>How many options one recorded cycle holds.</summary>
        public int OptionCapacity { get; }
    }
}
