using System.Collections.Generic;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     Watches one brain, keeping the cycle every reasoner monitor in its tree stamps its records with, and the
    ///     monitors themselves in tree order.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A host attaches one through <see cref="Brains.Brain.Monitor" />, and <see cref="Factories.GaiaLoader" />
    ///         attaches one to every brain it loads when it carries a <see cref="MonitorSettings" />. Attaching walks the
    ///         tree, the root reasoner, its options, and the reasoners their subreasoner actions hold, and gives each reasoner
    ///         a <see cref="ReasonerMonitor" /> of this monitor's own, so a nested reasoner's history is read through
    ///         <see cref="ReasonerMonitors" /> without the tree being opened to the host. A monitor this one already made is
    ///         kept along with what it has recorded, and any other is replaced, so a monitor a host attached by hand does not
    ///         survive attaching a brain monitor. The walk runs again when the brain is enabled, so a tree finished after
    ///         attaching is still covered.
    ///     </para>
    ///     <para>
    ///         The brain's cycle is what lines the histories up. A nested reasoner runs only while its option is selected
    ///         and would otherwise count cycles of its own, so two rings from one brain could not be read against each other.
    ///     </para>
    /// </remarks>
    public sealed class BrainMonitor
    {
        private readonly List<ReasonerMonitor> _reasonerMonitors = new();

        /// <summary>Creates a monitor whose reasoner monitors record under the given settings.</summary>
        public BrainMonitor(MonitorSettings settings)
        {
            Settings = settings;
        }

        /// <summary>The settings every reasoner monitor this one makes records under.</summary>
        public MonitorSettings Settings { get; }

        /// <summary>How many cycles the brain has run since this monitor was attached. Advanced before the root reasoner runs.</summary>
        public long CycleIndex { get; private set; }

        /// <summary>The monitors on the brain's reasoners, the root first and then in tree order.</summary>
        public IReadOnlyList<ReasonerMonitor> ReasonerMonitors => _reasonerMonitors;

        /// <summary>
        ///     Gives every reasoner in the brain's tree a monitor, keeping the ones this monitor made and their histories.
        ///     Called by the brain.
        /// </summary>
        internal void Attach(Brain brain)
        {
            _reasonerMonitors.Clear();

            if (brain.RootReasoner != null)
            {
                Walk(brain.RootReasoner, string.Empty);
            }
        }

        /// <summary>Opens the next brain cycle. Called by the brain before its root reasoner runs.</summary>
        internal void BeginCycle()
        {
            CycleIndex++;
        }

        /// <summary>Monitors the reasoner and every reasoner beneath it, each under the path of the options above it.</summary>
        private void Walk(ReasonerBase reasoner, string path)
        {
            ReasonerMonitor? monitor = reasoner.Monitor;

            if (monitor == null || !ReferenceEquals(monitor.BrainMonitor, this))
            {
                monitor = new ReasonerMonitor(Settings)
                {
                    BrainMonitor = this,
                    Path = path,
                };
                reasoner.Monitor = monitor;
            }

            _reasonerMonitors.Add(monitor);

            foreach (OptionBase option in reasoner.OptionList)
            {
                foreach (ActionBase action in option.Actions)
                {
                    if (action is SubreasonerAction { Reasoner: { } nested })
                    {
                        Walk(nested, path.Length == 0 ? option.Name.Text : path + "/" + option.Name.Text);
                    }
                }
            }
        }
    }
}
