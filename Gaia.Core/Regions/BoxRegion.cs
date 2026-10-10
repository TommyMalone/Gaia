using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A box between two corners measured from its Origin, which takes height into account.</summary>
    /// <remarks>
    ///     <see cref="RectangleRegion" /> with the Y axis included. TryGetRandomLocalPosition never fails, since every
    ///     point of the box is inside it.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class BoxRegion : RegionBase
    {
        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.Regions,
            "A box aligned with the world's axes.");

        private static readonly AttributeDescription MinAttribute = Builder.Required("Min", AttributeKind.Vector3,
            "The corner with the smallest X, Y and Z.");

        private static readonly AttributeDescription MaxAttribute = Builder.Required("Max", AttributeKind.Vector3,
            "The corner with the largest X, Y and Z.");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Creates a box whose corners are set through Min and Max.</summary>
        public BoxRegion()
        {
        }

        /// <summary>Creates a box reaching the given extents out from a center.</summary>
        /// <param name="center">Where the middle of the box is.</param>
        /// <param name="extents">How far the box reaches from center along each axis, half of its size in each.</param>
        public BoxRegion(Vector3 center, Vector3 extents)
        {
            Min = center - extents;
            Max = center + extents;
        }

        /// <summary>The corner with the smallest X, Y and Z, measured from Origin.</summary>
        public Vector3 Min { get; set; }

        /// <summary>The corner with the largest X, Y and Z, measured from Origin.</summary>
        public Vector3 Max { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadVector3(MinAttribute.Name, out Vector3 min) ||
                !node.ReadVector3(MaxAttribute.Name, out Vector3 max))
            {
                return false;
            }

            Min = min;
            Max = max;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return local.X >= Min.X && local.X <= Max.X && local.Y >= Min.Y && local.Y <= Max.Y && local.Z >= Min.Z &&
                   local.Z <= Max.Z;
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            local = MathHelpers.RandomInBox(RandomSource, Min, Max);

            return true;
        }
    }
}
