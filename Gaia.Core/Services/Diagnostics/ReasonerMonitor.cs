using System.Collections.Generic;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     Watches one reasoner, recording what its options scored and which it selected each cycle into a
    ///     <see cref="DecisionHistory" /> of its own.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A monitor is engineer-facing and is attached from code, never from configuration. A
    ///         <see cref="Diagnostics.BrainMonitor" /> makes one for every reasoner in a brain's tree, and a host debugging a
    ///         bare reasoner sets <see cref="ReasonerBase.Monitor" /> by hand. The reasoner calls <see cref="OnCycle" /> once
    ///         per cycle after it has chosen, and pays one null check per cycle when it has no monitor.
    ///     </para>
    ///     <para>
    ///         A cycle is stamped with the brain's cycle when a brain monitor made this one, so the histories of every
    ///         reasoner in a brain line up, and with this monitor's own count otherwise. A cycle holds the first
    ///         OptionCapacity options and drops the rest, and the monitor warns the first time that happens, so a partial
    ///         cycle is not read as a whole one.
    ///     </para>
    /// </remarks>
    public class ReasonerMonitor
    {
        private long _cycleIndex;
        private bool _truncationWarningIssued;

        /// <summary>Creates a monitor recording under the given settings.</summary>
        public ReasonerMonitor(MonitorSettings settings)
        {
            Settings = settings;
            History = new DecisionHistory(settings.CycleCapacity, settings.OptionCapacity);
        }

        /// <summary>The settings this monitor records under.</summary>
        public MonitorSettings Settings { get; }

        /// <summary>The ring of recent cycles this monitor has recorded.</summary>
        public DecisionHistory History { get; }

        /// <summary>The brain monitor that made this one, whose cycle stamps each record, or null for a monitor attached by hand.</summary>
        public BrainMonitor? BrainMonitor { get; internal set; }

        /// <summary>
        ///     The names of the options above this monitor's reasoner, root first and joined with a slash, so a report says
        ///     where in the tree it came from. Empty for a root reasoner and for a monitor attached by hand.
        /// </summary>
        public string Path { get; internal set; } = string.Empty;

        /// <summary>Records the cycle the reasoner has just decided. Called by the reasoner after Think.</summary>
        /// <remarks>
        ///     Everything here iterates the concrete option list and writes into storage allocated at construction, so a
        ///     recorded cycle allocates nothing and AllocationSpec holds it to that. The truncation warning builds its line once,
        ///     for a sink enabled at warning severity.
        /// </remarks>
        internal void OnCycle(ReasonerBase reasoner)
        {
            _cycleIndex++;

            List<OptionBase> options = reasoner.OptionList;

            if (!_truncationWarningIssued && options.Count > History.OptionCapacity)
            {
                _truncationWarningIssued = true;

                if (reasoner.LogSink.IsEnabled(LogSeverity.Warning))
                {
                    string where = Path.Length == 0 ? string.Empty : Path + ", ";

                    reasoner.LogSink.LogWarning(reasoner.LogChannel,
                        $"{where}the decision history records {History.OptionCapacity} of {options.Count} options, " +
                        "the rest go unrecorded");
                }
            }

            History.BeginCycle(BrainMonitor?.CycleIndex ?? _cycleIndex);

            foreach (OptionBase option in options)
            {
                History.Record(new DecisionRecord(option.Name, option.Rank, option.WeightBeforeScreening,
                    option.PassesScreening));
            }

            History.EndCycle(reasoner.LastDecisionRoll, reasoner.SelectedOption?.Name ?? AiString.Empty);
        }
    }
}
