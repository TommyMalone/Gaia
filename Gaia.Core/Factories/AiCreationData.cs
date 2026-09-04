using Gaia.Core.Blackboards;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     Everything a component needs to construct itself, the specification node it loads from, the factories it
    ///     builds children through, and the services a factory-built component uses.
    /// </summary>
    /// <remarks>
    ///     A composite descends by calling <see cref="ForChild" /> with one of its node's children, so the whole tree is
    ///     built from one creation data that only ever swaps its node. Contextual back-references arrive as component loading
    ///     needs them, the blackboards first.
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class AiCreationData
    {
        private static readonly AiString BrainStoreName = "Brain";
        private static readonly AiString StoreAttribute = "Store";
        private static readonly AiString WorldStoreName = "World";

        /// <param name="node">The specification node the component loads from.</param>
        /// <param name="factories">The factories a composite builds its children through.</param>
        internal AiCreationData(SpecificationNode node, FactorySet factories)
        {
            Node = node;
            Factories = factories;
        }

        /// <summary>The specification node the component loads from.</summary>
        public SpecificationNode Node { get; }

        /// <summary>The factories a composite builds its children through.</summary>
        public FactorySet Factories { get; }

        /// <summary>Where a component built from this data reports, pulled by its Init.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>The character the components being built belong to, pulled by their Inits.</summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>Where a component that measures time reads the clock, pulled by its Init.</summary>
        public ITimeSource TimeSource { get; internal set; } = new SystemTimeSource();

        /// <summary>Where a component that rolls reads its randomness, pulled by its Init.</summary>
        public IRandomSource RandomSource { get; internal set; } = new SystemRandomSource();

        /// <summary>The blackboard of the brain being built, or null until a host supplies one.</summary>
        public BrainBlackboard? BrainBlackboard { get; internal set; }

        /// <summary>The blackboard every character shares, or null until a host supplies one.</summary>
        public WorldBlackboard? WorldBlackboard { get; internal set; }

        /// <summary>The actor the brain being built belongs to, or null until a host supplies one.</summary>
        public Actor? Self { get; internal set; }

        /// <summary>The creation data for one of this node's children, the same everything with the child's node.</summary>
        public AiCreationData ForChild(SpecificationNode childNode)
        {
            return new AiCreationData(childNode, Factories)
            {
                LogSink = LogSink,
                LogChannel = LogChannel,
                TimeSource = TimeSource,
                RandomSource = RandomSource,
                BrainBlackboard = BrainBlackboard,
                WorldBlackboard = WorldBlackboard,
                Self = Self,
            };
        }

        /// <summary>Reads the node's optional Store attribute, which picks between the blackboards.</summary>
        /// <remarks>
        ///     The brain blackboard is the default, matching where a character's own knowledge lives. A store the data does
        ///     not know is a reported error.
        /// </remarks>
        internal bool TryReadStore(SpecificationNode node, out DataStoreBase? store)
        {
            store = BrainBlackboard;

            if (!node.TryGetString(StoreAttribute, out string text))
            {
                return true;
            }

            AiString storeName = text;

            if (storeName == BrainStoreName)
            {
                return true;
            }

            if (storeName == WorldStoreName)
            {
                if (WorldBlackboard == null)
                {
                    ReportError($"Line {node.LineNumber}, the {node.Name.Text} element's Store is World and no world " +
                                "blackboard was supplied");
                    return false;
                }

                store = WorldBlackboard;
                return true;
            }

            ReportError(
                $"Line {node.LineNumber}, the {node.Name.Text} element's Store is not Brain or World, it reads \"{text}\"");
            return false;
        }

        /// <summary>Reports a loading error against the node, filed under the node's source document name.</summary>
        internal void ReportError(string message)
        {
            LogSink.LogError(Node.SourceDocumentName, message);
        }

        /// <summary>Reports a loading warning against the node, filed under the node's source document name.</summary>
        internal void ReportWarning(string message)
        {
            LogSink.LogWarning(Node.SourceDocumentName, message);
        }
    }
}
