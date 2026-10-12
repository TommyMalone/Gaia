using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A ball around an origin, which takes height into account.</summary>
    /// <remarks>
    ///     <para>A turn leaves the ball as it was, and a Scale that differs along its axes stretches it into an ellipsoid.</para>
    ///     <para>
    ///         A random position is found by guessing inside the cube around the ball and keeping the first guess inside the
    ///         ball, so it can fail, though a ball fills over half its cube and <see cref="MathHelpers.MaxSampleAttempts" />
    ///         is generous.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class SphereRegion : RegionBase
    {
        private static readonly DescriptionBuilder Builder =
            new DescriptionBuilder(ConceptualAbstraction.Regions, "A ball.").Add(OriginAttribute);

        private static readonly AttributeDescription RadiusAttribute = Builder.Required("Radius", AttributeKind.Float,
            "How far the ball reaches from its middle.");

        internal static readonly ComponentDescription Description = Builder.Add(OriginTargetSlot).Build();

        /// <summary>How far the region reaches from its origin.</summary>
        public float Radius { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!LoadOriginTarget(creationData, out TargetBase? originTarget))
            {
                return false;
            }

            OriginTarget = originTarget;

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
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector3 reach = new(Radius, Radius, Radius);

            return TryRejectionSample(Vector3.Zero - reach, reach, out local);
        }
    }
}
