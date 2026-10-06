using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>An area of the world that a configuration can name, such as a kill zone or a line of retreat.</summary>
    /// <remarks>
    ///     <para>
    ///         A region answers two questions, whether a position is inside it and where a random position inside it would
    ///         be. The second is allowed to fail, because a shape that has to guess and check can run out of attempts, and the
    ///         caller has to be told rather than handed a position outside the region.
    ///     </para>
    ///     <para>
    ///         RandomSource is injected the same way it is for <see cref="DualUtilityReasoner" />, so a test can make
    ///         sampling deterministic.
    ///     </para>
    ///     <para>
    ///         Regions are built in code as well as loaded, so their values and RandomSource are public and a host may build
    ///         its shapes in code, the way a game sorts contacts into the entity lists its considerations read. Loading fills
    ///         the same members.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6, Listing 8.6</seealso>
    public abstract class RegionBase : IAiComponent
    {
        /// <summary>
        ///     Describes the optional OriginTarget child <see cref="LoadOriginTarget" /> reads by, for the description of
        ///     every shape that calls it.
        /// </summary>
        /// <remarks>
        ///     Circle, Cylinder and Sphere read this child and the other six shapes do not. Two of the three are flat and the
        ///     third is not, so the slot cannot ride DescribeFlatShape, which gives every flat shape its Origin and Rotation
        ///     attributes, and each shape that reads it adds the slot to its own description.
        /// </remarks>
        protected static readonly SlotDescription OriginTargetSlot = new("OriginTarget", "OriginTarget",
            ConceptualAbstraction.Targets, 0, 1);

        /// <summary>Where the region draws the numbers that pick a random position.</summary>
        public IRandomSource RandomSource { get; set; } = new Xoshiro128RandomSource();

        /// <summary>Loads this region's configuration from the creation data's node.</summary>
        /// <remarks>Every region samples through the creation data's random source, so the base takes it before OnInit.</remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            RandomSource = creationData.RandomSource;
            return OnInit(creationData);
        }

        /// <summary>Loads the derived region's own configuration.</summary>
        /// <remarks>
        ///     The factory has already refused whatever the description does not name, and the default returns true, so a
        ///     region with nothing of its own to load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Loads the node's OriginTarget child, if it has one, for the shapes that can follow a target.</summary>
        /// <remarks>
        ///     The factory has already refused any other child and a second OriginTarget against the shape's description, so
        ///     every child met here is the one OriginTarget.
        /// </remarks>
        /// <param name="creationData">The creation data whose node carries the OriginTarget child, and whose factories build it.</param>
        /// <param name="originTarget">The loaded target, or null when the node has no OriginTarget child.</param>
        /// <returns>True when the children loaded. False after reporting what went wrong.</returns>
        protected static bool LoadOriginTarget(AiCreationData creationData, out TargetBase? originTarget)
        {
            SpecificationNode node = creationData.Node;

            originTarget = null;

            foreach (SpecificationNode child in node.Children)
            {
                if (!creationData.Factories.Targets.TryCreate(creationData.ForChild(child), out originTarget))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Tests whether a position is inside the region.</summary>
        public abstract bool IsInRegion(Vector3 position);

        /// <summary>Picks a random position inside the region.</summary>
        /// <returns>True if a position was found. Some shapes can fail, and then the position is unspecified.</returns>
        public abstract bool TryGetRandomPosition(out Vector3 position);
    }
}
