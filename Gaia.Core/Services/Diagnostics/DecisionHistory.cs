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
    ///         it runs. Once the ring is full each new cycle overwrites the oldest, and a cycle with more options than
    ///         OptionCapacity records the first OptionCapacity of them.
    ///     </para>
    ///     <para>
    ///         That storage is the cost of attaching one. The default capacities cost 56 KiB per history, 57,344 bytes
    ///         across the two arrays, a figure DecisionHistorySpec measures. A host that attaches one to every reasoner of
    ///         five hundred characters pays that once per reasoner, most of it recording nothing that is read. Monitors are
    ///         for the brains under investigation.
    ///     </para>
    /// </remarks>
    public sealed class DecisionHistory
    {
        private readonly DecisionCycle[] _cycles;
        private readonly DecisionRecord[] _records;

        private int _newestCycle = -1;
        private int _recordsInOpenCycle;

        /// <summary>Creates a history holding the given number of cycles of the given number of options each.</summary>
        public DecisionHistory(int cycleCapacity = 256, int optionCapacity = 8)
        {
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
        ///     One recorded cycle, by age. An age of 0 is the newest cycle and CycleCount - 1 the oldest. Ages outside that
        ///     range are not checked.
        /// </summary>
        public DecisionCycle GetCycle(int age)
        {
            return _cycles[CycleSlot(age)];
        }

        /// <summary>One option's record within the cycle of the given age, by position. Neither argument is checked.</summary>
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
