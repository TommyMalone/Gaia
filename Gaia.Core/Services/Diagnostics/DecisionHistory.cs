using System;
using Gaia.Core.Strings;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     A ring buffer of a reasoner's recent decisions, one entry per cycle holding what its options scored, the roll,
    ///     and the option selected.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The history answers the tuning question the log cannot, what rank and weight each option had in a decision
    ///         that already happened. A <see cref="ReasonerMonitor" /> owns one and writes it every cycle, and a host reads it
    ///         through the monitor when something looks wrong.
    ///     </para>
    ///     <para>
    ///         All storage is allocated at construction and written in place, so recording costs no allocation however long
    ///         it runs.
    ///     </para>
    ///     <para>
    ///         That storage is the cost of attaching one. At the default capacities the two arrays cost 56 KiB per history,
    ///         allocated whether or not anything reads them, so a monitor is meant for the brains under investigation rather
    ///         than every brain a host runs. DecisionHistorySpec measures that figure.
    ///     </para>
    /// </remarks>
    public sealed class DecisionHistory
    {
        private readonly DecisionCycle[] _cycles;
        private readonly DecisionRecord[] _records;

        private int _newestCycle = -1;
        private int _recordsInOpenCycle;

        /// <summary>Creates a history holding the given number of cycles of the given number of options each.</summary>
        /// <exception cref="ArgumentOutOfRangeException">cycleCapacity or optionCapacity is below 1.</exception>
        public DecisionHistory(int cycleCapacity = 256, int optionCapacity = 8)
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

            _cycles = new DecisionCycle[cycleCapacity];
            _records = new DecisionRecord[cycleCapacity * optionCapacity];
        }

        /// <summary>How many cycles the ring holds before overwriting its oldest.</summary>
        public int CycleCapacity { get; }

        /// <summary>How many options one cycle can record.</summary>
        public int OptionCapacity { get; }

        /// <summary>How many cycles are currently recorded, from 0 up to CycleCapacity.</summary>
        public int CycleCount { get; private set; }

        /// <summary>
        ///     One recorded cycle, the given number of cycles back from the newest. 0 is the newest cycle and CycleCount - 1
        ///     the oldest, so a history with no cycles recorded refuses every call.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">cyclesBack is below 0, or CycleCount or more.</exception>
        public DecisionCycle GetCycleFromNewest(int cyclesBack)
        {
            if (cyclesBack < 0 || cyclesBack >= CycleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(cyclesBack), cyclesBack,
                    $"a count of cycles back from the newest is at least 0 and below the {CycleCount} cycles recorded");
            }

            return _cycles[CycleSlot(cyclesBack)];
        }

        /// <summary>One option's record within the cycle the given number of cycles back from the newest, by position.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <see cref="GetCycleFromNewest" /> refuses cyclesBack, or the optionIndex
        ///     is below 0, or the cycle's RecordCount or more.
        /// </exception>
        public DecisionRecord GetRecordFromNewest(int cyclesBack, int optionIndex)
        {
            DecisionCycle cycle = GetCycleFromNewest(cyclesBack);

            if (optionIndex < 0 || optionIndex >= cycle.RecordCount)
            {
                throw new ArgumentOutOfRangeException(nameof(optionIndex), optionIndex,
                    $"an option index is at least 0 and below the {cycle.RecordCount} records the cycle holds");
            }

            return _records[CycleSlot(cyclesBack) * OptionCapacity + optionIndex];
        }

        /// <summary>Opens the next cycle, overwriting the oldest once the ring is full. Called by the monitor.</summary>
        internal void BeginCycle(long cycleIndex)
        {
            _newestCycle = (_newestCycle + 1) % CycleCapacity;
            _recordsInOpenCycle = 0;

            if (CycleCount < CycleCapacity)
            {
                CycleCount++;
            }

            _cycles[_newestCycle] = new DecisionCycle(cycleIndex, 0f, AiString.Empty, 0);
        }

        /// <summary>Records one option into the open cycle. Options past OptionCapacity are not recorded.</summary>
        internal void Record(in DecisionRecord record)
        {
            if (_recordsInOpenCycle >= OptionCapacity)
            {
                return;
            }

            _records[_newestCycle * OptionCapacity + _recordsInOpenCycle] = record;
            _recordsInOpenCycle++;
        }

        /// <summary>Closes the open cycle with the roll that decided it and the name of the option selected.</summary>
        internal void EndCycle(float roll, AiString selectedName)
        {
            DecisionCycle open = _cycles[_newestCycle];

            _cycles[_newestCycle] = new DecisionCycle(open.CycleIndex, roll, selectedName, _recordsInOpenCycle);
        }

        /// <summary>The ring slot holding the cycle the given number of cycles back from the newest.</summary>
        private int CycleSlot(int cyclesBack)
        {
            return (_newestCycle - cyclesBack + CycleCapacity) % CycleCapacity;
        }
    }
}
