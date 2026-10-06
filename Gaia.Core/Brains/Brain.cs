using System;
using Gaia.Core.Blackboards;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Log;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Brains
{
    /// <summary>The decision-making logic of one character, holding its top-level reasoner and a blackboard of its own.</summary>
    /// <remarks>
    ///     The chapter describes the brain only in passing, as the thing an actor contains and the thing its reasoner
    ///     reports to, so what it holds has been derived from what the rest of the architecture needs from it. A
    ///     <see cref="BrainBlackboard" /> is where contacts and threats live and where components of one character share
    ///     values, so the brain owns it. RootReasoner is what makes the character act at all, so the brain enables and updates
    ///     it.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.3.1</seealso>
    public class Brain
    {
        private static readonly DescriptionBuilder Builder = new("The decisions of one character.");

        private static readonly SlotDescription ReasonerSlot =
            Builder.Slot("Reasoner", ConceptualAbstraction.Reasoners, 0, 1);

        /// <summary>Describes the Brain element, which <see cref="FactorySet.Root" /> reports, since no factory builds a brain.</summary>
        internal static readonly ComponentDescription Description = Builder.Build();

        private BrainMonitor? _monitor;

        /// <summary>
        ///     Creates a brain that decides nothing until a configuration supplies its reasoner. A host assembling a brain in
        ///     code uses the constructor that takes one, since RootReasoner cannot be set afterward.
        /// </summary>
        public Brain()
        {
        }

        /// <summary>Creates a brain over the given reasoner, for a host assembling its structure in code.</summary>
        public Brain(ReasonerBase rootReasoner)
        {
            RootReasoner = rootReasoner;
        }

        /// <summary>The values the components of this character share among themselves.</summary>
        /// <remarks>
        ///     The brain makes its own blackboard, and <see cref="Factories.GaiaLoader" /> accepts a kept one at load, so a
        ///     character whose brain is swapped keeps what it knew. What survives a swap is decided by the host, since the brain
        ///     never clears what it is handed.
        /// </remarks>
        public BrainBlackboard Blackboard { get; internal set; } = new();

        /// <summary>The reasoner at the top of this character's decision tree. Null means the brain decides nothing.</summary>
        public ReasonerBase? RootReasoner { get; internal set; }

        /// <summary>Where this brain reports taking control of its character and giving it up.</summary>
        public ILogSink LogSink { get; set; } = new TraceLogSink();

        /// <summary>The character this brain belongs to, which its entries are filed under.</summary>
        public AiString LogChannel { get; set; } = AiString.Empty;

        /// <summary>True while the brain is in control of its character.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>What this brain does with an exception a component throws during a cycle. Rethrow by default.</summary>
        /// <remarks>
        ///     Public set because a host assembles brains in code as often as it loads them, and
        ///     <see cref="Factories.GaiaLoader" /> stamps its own policy on every brain it loads. Which way a fault surfaces is a
        ///     stance a whole process takes, development or shipped, rather than a property of one character.
        /// </remarks>
        public FaultPolicy FaultPolicy { get; set; } = FaultPolicy.Rethrow;

        /// <summary>
        ///     The exception that stopped this brain under <see cref="Brains.FaultPolicy.Contain" />, or null while it is
        ///     deciding.
        /// </summary>
        /// <remarks>
        ///     Set once and never cleared, so a host that keeps running can count its faulted brains and read why each one
        ///     stopped, and a faulted brain refuses Enable. A character that has faulted is replaced, not restarted.
        /// </remarks>
        public Exception? Fault { get; private set; }

        /// <summary>The monitor watching this brain and every reasoner in its tree, or null when nothing is watching.</summary>
        /// <remarks>
        ///     Setting one attaches it, which walks the tree and gives each reasoner a <see cref="ReasonerMonitor" />, and
        ///     the walk runs again at Enable so a tree finished after attaching is covered. Diagnostics are attached from code, by
        ///     a host or by <see cref="Factories.GaiaLoader" /> for every brain it loads when it carries a
        ///     <see cref="MonitorSettings" />, and never from configuration.
        /// </remarks>
        public BrainMonitor? Monitor
        {
            get => _monitor;
            set
            {
                _monitor = value;
                value?.Attach(this);
            }
        }

        /// <summary>Loads this brain's configuration from the creation data's node.</summary>
        /// <remarks>
        ///     <para>
        ///         The brain stamps the tree beneath it. Its LogChannel comes from the creation data's Self actor when one is
        ///         supplied, and its own Blackboard becomes the BrainBlackboard every descendant loads against, so a host never
        ///         wires either by hand.
        ///     </para>
        ///     <para>
        ///         No factory builds a brain, so Init holds the Brain element to its description itself, before the children
        ///         load and again after. A Brain element with no Reasoner child loads, matching the runtime rule that a brain may
        ///         decide nothing, and Enable warns about it.
        ///     </para>
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!Description.RefuseUndescribed(creationData, true))
            {
                return false;
            }

            SpecificationNode node = creationData.Node;

            LogSink = creationData.LogSink;
            LogChannel = creationData.Self?.Name ?? creationData.LogChannel;

            // The blackboard reports through the brain's sink under the brain's channel, so a kind mismatch on a
            // character's own store is filed under that character. A kept blackboard is restamped for its new brain.
            Blackboard.LogSink = LogSink;
            Blackboard.LogChannel = LogChannel;

            // Every child is the one Reasoner, since the description check refused anything else.
            foreach (SpecificationNode child in node.Children)
            {
                AiCreationData childData = creationData.ForChild(child);
                childData.LogChannel = LogChannel;
                childData.BrainBlackboard = Blackboard;

                if (!creationData.Factories.Reasoners.TryCreate(childData, out ReasonerBase? rootReasoner))
                {
                    return false;
                }

                RootReasoner = rootReasoner;
            }

            return Description.RefuseShortSlots(creationData);
        }

        /// <summary>Hands control to the brain, which starts RootReasoner deciding.</summary>
        /// <remarks>
        ///     Enabling and disabling live here rather than on the actor so that a character can keep its actor while the
        ///     player has control, with its brain disabled, or with no brain at all.
        /// </remarks>
        public void Enable()
        {
            if (IsEnabled)
            {
                return;
            }

            if (Fault != null)
            {
                LogSink.LogWarning(LogChannel, "brain faulted and stays stopped until it is replaced");
                return;
            }

            IsEnabled = true;

            if (RootReasoner == null)
            {
                LogSink.LogWarning(LogChannel, "brain enabled with no root reasoner");
            }
            else
            {
                LogSink.LogMessage(LogChannel, "brain enabled");
            }

            _monitor?.Attach(this);
            RootReasoner?.Enable();
        }

        /// <summary>Takes control away from the brain, which stops whatever RootReasoner was running.</summary>
        /// <remarks>
        ///     Disable disables RootReasoner, which deselects whatever it was running, so the whole tree beneath it is torn
        ///     down in one call.
        /// </remarks>
        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            LogSink.LogMessage(LogChannel, "brain disabled");
            RootReasoner?.Disable();
        }

        /// <summary>Runs one decision cycle, if IsEnabled.</summary>
        /// <remarks>
        ///     A cycle is one run of the decision hierarchy, the root reasoner's Update and everything it reaches, and it is
        ///     the AI's unit of time. A tick is one iteration of the host's own loop, and a host runs a brain's cycles as often as
        ///     it chooses, once per tick or less.
        /// </remarks>
        public void Update()
        {
            if (!IsEnabled)
            {
                return;
            }

            if (FaultPolicy == FaultPolicy.Rethrow)
            {
                RunCycle();
                return;
            }

            try
            {
                RunCycle();
            }
            catch (Exception exception)
            {
                Contain(exception);
            }
        }

        /// <summary>Advances the monitor's cycle and runs the root reasoner, which is one cycle.</summary>
        private void RunCycle()
        {
            _monitor?.BeginCycle();
            RootReasoner?.Update();
        }

        /// <summary>Keeps the fault, reports it, and stops the brain, guarding the stop against the same fault again.</summary>
        private void Contain(Exception exception)
        {
            Fault = exception;
            LogSink.LogError(LogChannel,
                $"a component threw and the brain is stopped, {exception.GetType().Name}: {exception.Message}");

            try
            {
                Disable();
            }
            catch (Exception second)
            {
                // Disable deselects whatever was running, and an action that threw in Update usually throws again in
                // Deselect for the same reason. IsEnabled was cleared before Disable reached the tree, so the brain is
                // stopped either way, and the tree is left as it fell.
                LogSink.LogError(LogChannel,
                    $"the brain threw again while stopping, {second.GetType().Name}: {second.Message}");
            }
        }
    }
}
