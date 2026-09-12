using Gaia.Core.Blackboards;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
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
        private static readonly AiString ReasonerElement = "Reasoner";

        /// <summary>The values the components of this character share among themselves.</summary>
        /// <remarks>
        ///     The brain makes its own blackboard, and <see cref="Factories.GaiaLoader" /> accepts a kept one at load, so a
        ///     character whose brain is swapped keeps what it knew. What survives a swap is decided by the host, since the brain
        ///     never clears what it is handed. Blackboard swapping is supported primarily to allow for runtime editing and
        ///     iteration.
        /// </remarks>
        public BrainBlackboard Blackboard { get; internal set; } = new();

        /// <summary>The reasoner at the top of this character's decision tree. Null means the brain decides nothing.</summary>
        public ReasonerBase? RootReasoner { get; internal set; }

        /// <summary>Where this brain reports taking control of its character and giving it up.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>The character this brain belongs to, which its entries are filed under.</summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>True while the brain is in control of its character.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>Loads this brain's configuration from the creation data's node.</summary>
        /// <remarks>
        ///     <para>
        ///         The brain stamps the tree beneath it. Its LogChannel comes from the creation data's Self actor when one is
        ///         supplied, and its own Blackboard becomes the BrainBlackboard every descendant loads against, so a host never
        ///         wires either by hand.
        ///     </para>
        ///     <para>
        ///         A Brain element with no Reasoner child loads, matching the runtime rule that a brain may decide nothing, and
        ///         Enable warns about it. A second Reasoner child is refused, because silently keeping one of them would ship half
        ///         the author's tree.
        ///     </para>
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            LogSink = creationData.LogSink;
            LogChannel = creationData.Self?.Name ?? creationData.LogChannel;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != ReasonerElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (RootReasoner != null)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element takes only one Reasoner element");
                    return false;
                }

                AiCreationData childData = creationData.ForChild(child);
                childData.LogChannel = LogChannel;
                childData.BrainBlackboard = Blackboard;

                if (!creationData.Factories.Reasoners.TryCreate(childData, out ReasonerBase? rootReasoner))
                {
                    return false;
                }

                RootReasoner = rootReasoner;
            }

            return true;
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

            IsEnabled = true;

            if (RootReasoner == null)
            {
                LogSink.LogWarning(LogChannel, "brain enabled with no root reasoner");
            }
            else
            {
                LogSink.LogMessage(LogChannel, "brain enabled");
            }

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
        public void Update()
        {
            if (!IsEnabled)
            {
                return;
            }

            RootReasoner?.Update();
        }
    }
}
