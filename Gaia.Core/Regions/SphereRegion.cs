using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A ball around an origin, which takes height into account.</summary>
    /// <remarks>A turn leaves the ball as it was, and a Scale that differs along its axes stretches it into an ellipsoid.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class SphereRegion : RegionBase
    {
        private static readonly DescriptionBuilder Builder = DescribeShape("A ball.");

        private static readonly AttributeDescription RadiusAttribute = Builder.Required("Radius", AttributeKind.Float,
            "How far the ball reaches from its middle.");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>How far the region reaches from its origin.</summary>
        public float Radius { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadFloat(RadiusAttribute.Name, out float radius))
            {
                return false;
            }

            Radius = radius;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return local.SquaredMagnitude <= MathHelpers.SquaredReach(Radius, ToleranceInAnyDirection);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Guesses inside the cube around the ball and keeps the first guess inside it. The pick fails when the attempts
        ///     <see cref="RegionBase.TryRejectionSample" /> makes run out, which is unlikely, since a ball fills over half its
        ///     cube.
        /// </remarks>
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector3 reach = new(Radius, Radius, Radius);

            return TryRejectionSample(Vector3.Zero - reach, reach, out local);
        }
    }
}
