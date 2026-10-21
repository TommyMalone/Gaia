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

        /// <summary>One recorded cycle, by age. An age of 0 is the newest cycle and CycleCount - 1 the oldest.</summary>
        /// <remarks>
        ///     The age is not checked. An age from CycleCount to CycleCapacity - 1 reads a slot nothing has written, a
        ///     default DecisionCycle. A larger age wraps around the ring, so it reads the cycle CycleCapacity younger than it
        ///     names, until the slot it works out is below 0, where the array throws IndexOutOfRangeException, except at an age a
        ///     whole multiple of CycleCapacity beyond the last that wraps, which works out to slot 0 and reads it.
        /// </remarks>
        public DecisionCycle GetCycle(int age)
        {
            return _cycles[CycleSlot(age)];
        }

        /// <summary>One option's record within the cycle of the given age, by position.</summary>
        /// <remarks>
        ///     Neither argument is checked. The age reads as GetCycle reads it, and an optionIndex of OptionCapacity or more
        ///     reads the records of the slots after it in the array, or throws IndexOutOfRangeException past its end.
        /// </remarks>
        public DecisionRecord GetRecord(int age, int optionIndex)
        {
            return _records[CycleSlot(age) * OptionCapacity + optionIndex];
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

        /// <summary>The ring slot holding the cycle of the given age.</summary>
        private int CycleSlot(int age)
        {
            return (_newestCycle - age + CycleCapacity) % CycleCapacity;
        }
    }
}
