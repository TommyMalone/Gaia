using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>An area of the world that a configuration can name, such as a kill zone or a line of retreat.</summary>
    /// <remarks>
    ///     <para>
    ///         A region answers two questions, whether a position is inside it and where a random position inside it would
    ///         be. The second is allowed to fail, as Listing 8.6 notes, because a shape that has to guess and check can run
    ///         out of attempts, and the caller has to be told rather than handed a position outside the region.
    ///     </para>
    ///     <para>
    ///         RandomSource is injected the same way it is for <see cref="DualUtilityReasoner" />, so a spec can make
    ///         sampling deterministic.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6, Listing 8.6</seealso>
    public abstract class RegionBase : IAiComponent
    {
        /// <summary>Where the region draws the numbers that pick a random position.</summary>
        public IRandomSource RandomSource { get; internal set; } = new SystemRandomSource();

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived region's own configuration.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        protected abstract bool OnInit(AiCreationData creationData);

        /// <summary>Tests whether a position is inside the region.</summary>
        public abstract bool IsInRegion(Vector3 position);

        /// <summary>Picks a random position inside the region.</summary>
        /// <returns>True if a position was found. Some shapes can fail, and then the position is unspecified.</returns>
        public abstract bool TryGetRandomPosition(out Vector3 position);
    }
}
