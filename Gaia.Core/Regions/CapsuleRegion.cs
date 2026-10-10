using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A ball swept along a segment on its own up axis, which takes height into account.</summary>
    /// <remarks>
    ///     <para>
    ///         HalfHeight is half the capsule's full height, caps included, so the segment runs along local Y from Radius -
    ///         HalfHeight to HalfHeight - Radius. A position is inside if it is within Radius of the nearest point of the
    ///         segment, which caps the ends with hemispheres rather than cutting them flat. A HalfHeight under Radius leaves
    ///         no segment, and the capsule is the ball of Radius.
    ///     </para>
    ///     <para>
    ///         A random position is found by guessing inside the box around the capsule and keeping the first guess inside
    ///         it, as for <see cref="SphereRegion" />. A capsule fills at least as much of its box as a ball fills of its
    ///         cube, since a HalfHeight of Radius leaves exactly that ball, so the attempts are no more likely to run out.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CapsuleRegion : RegionBase
    {
        private static readonly DescriptionBuilder Builder =
            new DescriptionBuilder(ConceptualAbstraction.Regions, "A cylinder with a rounded top and bottom.").Add(
                OriginAttribute);

        private static readonly AttributeDescription RadiusAttribute = Builder.Required("Radius", AttributeKind.Float,
            "How far the region reaches from the segment.");

        private static readonly AttributeDescription HalfHeightAttribute = Builder.Required("HalfHeight",
            AttributeKind.Float,
            "Half the capsule's height, from its middle to the tip of either cap. A value under Radius makes the capsule the ball of Radius.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private float _radius;
        private float _radiusSquared;

        /// <summary>Half the capsule's full height along its own up, from its middle to the tip of either cap.</summary>
        /// <remarks>
        ///     A value under Radius leaves no segment, so the capsule is the ball of Radius and the tip of either cap is
        ///     Radius from its middle.
        /// </remarks>
        public float HalfHeight { get; set; }

        /// <summary>How far the region reaches from the segment.</summary>
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = value;

                // Squared once here rather than on every containment test.
                _radiusSquared = value * value;
            }
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadFloat(RadiusAttribute.Name, out float radius) ||
                !node.ReadFloat(HalfHeightAttribute.Name, out float halfHeight))
            {
                return false;
            }

            Radius = radius;
            HalfHeight = halfHeight;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            float segmentReach = MathF.Max(HalfHeight - Radius, 0f);
            float clampedY = Math.Clamp(local.Y, -segmentReach, segmentReach);
            Vector3 nearestOnSegment = new(0f, clampedY, 0f);

            return Vector3.SquaredDistance(nearestOnSegment, local) <= _radiusSquared;
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector3 reach = new(Radius, MathF.Max(HalfHeight, Radius), Radius);

            return TryRejectionSample(Vector3.Zero - reach, reach, out local);
        }
    }
}
