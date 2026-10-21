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
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class AiCreationData
    {
        /// <summary>
        ///     Describes the Store attribute that <see cref="TryReadStore" /> reads, and is added to the description of every
        ///     component that reads a store.
        /// </summary>
        internal static readonly AttributeDescription StoreAttribute = AttributeDescription.Enumeration<StoreKind>(
            "Store", "Store", nameof(StoreKind.Brain), "Which blackboard holds the value, Brain or World.");

        /// <summary>
        ///     Creates the creation data a load starts from, which <see cref="ForChild" /> then copies for every component
        ///     beneath it.
        /// </summary>
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

        /// <summary>
        ///     The name of the character the components being built belong to, which their Inits pull as the name their log
        ///     entries are filed under. Brain.Init sets it from Self when one is supplied.
        /// </summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>Where a component that measures time reads the clock, pulled by its Init.</summary>
        public ITimeSource TimeSource { get; internal set; } = new SystemTimeSource();

        /// <summary>Where a component that rolls reads its randomness, pulled by its Init.</summary>
        public IRandomSource RandomSource { get; internal set; } = new Xoshiro128RandomSource();

        /// <summary>
        ///     The blackboard of the <see cref="Brains.Brain" /> being built. The data a brain itself loads from carries
        ///     none, since the brain holds its blackboard as its own <see cref="Brains.Brain.Blackboard" />. Brain.Init sets that
        ///     on the data it hands its root reasoner, so everything beneath a brain has one.
        /// </summary>
        public BrainBlackboard? BrainBlackboard { get; internal set; }

        /// <summary>The blackboard every character shares, or null until a host supplies one.</summary>
        public WorldBlackboard? WorldBlackboard { get; internal set; }

        /// <summary>The actor the brain being built belongs to, or null until a host supplies one.</summary>
        public Actor? Self { get; internal set; }

        /// <summary>
        ///     The creation data for one of this node's children, every field copied and the node replaced by the child's. A
        ///     composite is a component that builds children of its own from its node's child elements, and it calls this with
        ///     each child element it builds from.
        /// </summary>
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
        ///     The brain blackboard is the default, matching where a character's own knowledge lives. A Store that is not
        ///     Brain or World is a reported error, and so is World when no world blackboard was supplied. Brain, or no Store at
        ///     all, takes the brain blackboard as it stands, null included, since a null store reads as one with nothing written
        ///     in it rather than as a mistake.
        /// </remarks>
        internal bool TryReadStore(SpecificationNode node, out DataStoreBase? store)
        {
            store = BrainBlackboard;
            StoreKind kind = StoreKind.Brain;

            if (node.HasAttribute(StoreAttribute.Name) && !node.ReadEnumeration(StoreAttribute.Name, out kind))
            {
                return false;
            }

            if (kind == StoreKind.Brain)
            {
                return true;
            }

            if (WorldBlackboard == null)
            {
                ReportError(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element's Store is World and no world blackboard was supplied");
                return false;
            }

            store = WorldBlackboard;
            return true;
        }

        /// <summary>Reports a loading error against the node, filed under the node's source document name.</summary>
        /// <remarks>
        ///     Public so that a game's component can refuse what it reads in the loader's own grammar. The message opens
        ///     "Line n, the Element element", and a clause of the component's own follows. Here n is the line of the element at
        ///     fault, and Element is that element's name as the document spells it.
        /// </remarks>
        public void ReportError(string message)
        {
            LogSink.LogError(Node.SourceDocumentName, message);
        }

        /// <summary>Reports a loading warning against the node, filed under the node's source document name.</summary>
        /// <remarks>Public for the same reason <see cref="ReportError" /> is, and the message opens the same way.</remarks>
        public void ReportWarning(string message)
        {
            LogSink.LogWarning(Node.SourceDocumentName, message);
        }
    }
}
