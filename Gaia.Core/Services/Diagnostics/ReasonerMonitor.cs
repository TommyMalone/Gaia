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
    ///         The monitor also names two shapes the cycle cannot refuse and loading cannot see, an option left done for a
    ///         whole window with nothing ending it and a reasoner that has selected nothing for one. Both windows are
    ///         <see cref="MonitorSettings" />, so a run is watched as closely as its host asks and a run with no monitor is
    ///         not watched at all.
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
        private int _cyclesNothingSelected;
        private int _cyclesSelectedAndDone;
        private OptionBase? _doneOption;
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
                    reasoner.LogSink.LogWarning(reasoner.LogChannel,
                        Located($"the decision history records {History.OptionCapacity} of {options.Count} options, " +
                                "the rest go unrecorded"));
                }
            }

            History.BeginCycle(BrainMonitor?.CycleIndex ?? _cycleIndex);

            foreach (OptionBase option in options)
            {
                History.Record(new DecisionRecord(option.Name, option.Rank, option.WeightBeforeScreening,
                    option.PassesScreening));
            }

            History.EndCycle(reasoner.LastDecisionRoll, reasoner.SelectedOption?.Name ?? AiString.Empty);

            WarnAboutDoneOption(reasoner);
            WarnAboutNothingSelected(reasoner);
        }

        /// <summary>Warns when the selected option has been done for a whole window with nothing ending it.</summary>
        /// <remarks>
        ///     The count belongs to one selection, so selecting a different option starts it over, and the warning fires once
        ///     each time the count reaches the window rather than once per cycle past it.
        /// </remarks>
        private void WarnAboutDoneOption(ReasonerBase reasoner)
        {
            OptionBase? selected = reasoner.SelectedOption;

            if (Settings.DoneWarningWindow <= 0 || selected == null || !selected.IsDone)
            {
                _doneOption = null;
                _cyclesSelectedAndDone = 0;
                return;
            }

            if (!ReferenceEquals(selected, _doneOption))
            {
                _doneOption = selected;
                _cyclesSelectedAndDone = 0;
            }

            _cyclesSelectedAndDone++;

            if (_cyclesSelectedAndDone == Settings.DoneWarningWindow && reasoner.LogSink.IsEnabled(LogSeverity.Warning))
            {
                reasoner.LogSink.LogWarning(reasoner.LogChannel,
                    Located($"the {selected.Name.Text} option has been done for {Settings.DoneWarningWindow} cycles " +
                            "with nothing ending it, a finished veto may be missing"));
            }
        }

        /// <summary>Warns when a reasoner with work left has had options and no selection for a whole window.</summary>
        /// <remarks>
        ///     A reasoner that ran out of work, a finished sequence, selects nothing by design until its parent ends it, so
        ///     nothing selected is only worth a warning while there is work left. The count runs over the cycles the reasoner
        ///     actually ran, so for a nested reasoner one window spans the activations of its option.
        /// </remarks>
        private void WarnAboutNothingSelected(ReasonerBase reasoner)
        {
            if (Settings.NothingSelectedWarningWindow <= 0 || reasoner.SelectedOption != null ||
                reasoner.OptionList.Count == 0 || reasoner.IsDone)
            {
                _cyclesNothingSelected = 0;
                return;
            }

            _cyclesNothingSelected++;

            if (_cyclesNothingSelected == Settings.NothingSelectedWarningWindow &&
                reasoner.LogSink.IsEnabled(LogSeverity.Warning))
            {
                reasoner.LogSink.LogWarning(reasoner.LogChannel,
                    Located($"nothing has been selectable for {Settings.NothingSelectedWarningWindow} cycles, " +
                            "every option is vetoed or scores nothing"));
            }
        }

        /// <summary>Prefixes a report with this monitor's path, so a report from a nested reasoner says where it came from.</summary>
        private string Located(string message)
        {
            return Path.Length == 0 ? message : Path + ", " + message;
        }
    }
}
