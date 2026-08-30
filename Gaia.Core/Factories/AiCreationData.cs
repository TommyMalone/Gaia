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
    ///     built from one creation data that only ever swaps its node. Contextual back-references such as blackboards arrive
    ///     here once a component's loading needs them.
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class AiCreationData
    {
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

        /// <summary>The creation data for one of this node's children, the same everything with the child's node.</summary>
        public AiCreationData ForChild(SpecificationNode childNode)
        {
            return new AiCreationData(childNode, Factories)
            {
                LogSink = LogSink,
                LogChannel = LogChannel,
                TimeSource = TimeSource,
                RandomSource = RandomSource,
            };
        }
    }
}
