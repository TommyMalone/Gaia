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
        ///     Three of the shipped shapes call LoadOriginTarget and the other six refuse every child, so this piece is added
        ///     by the shape that reads it rather than handed out with a builder the way the frame's attributes are.
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
        ///     The base refuses any child element and otherwise reports true, so a region with nothing of its own to load
        ///     leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return creationData.RefuseChildren();
        }

        /// <summary>Loads the node's OriginTarget child, if it has one, for the shapes that can follow a target.</summary>
        /// <remarks>
        ///     Any other child is refused, and so is a second OriginTarget, since silently keeping one of two would ship half
        ///     of what was written.
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
                if (child.Name != OriginTargetSlot.Name)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (originTarget != null)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element takes only one OriginTarget element");
                    return false;
                }

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
