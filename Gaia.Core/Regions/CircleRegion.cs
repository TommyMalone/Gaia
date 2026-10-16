using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A disc in its own plane, which ignores height.</summary>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CircleRegion : PlanarRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeFlatShape("A disc on the ground, which ignores height.");

        private static readonly AttributeDescription RadiusAttribute = Builder.Required("Radius", AttributeKind.Float,
            "How far the disc reaches from its middle.");

        internal static readonly ComponentDescription Description = Builder.Add(OriginTargetSlot).Build();

        /// <summary>How far the region reaches from its axis.</summary>
        public float Radius { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!LoadOriginTarget(creationData, out TargetBase? originTarget))
            {
                return false;
            }

            OriginTarget = originTarget;

            if (!creationData.Node.ReadFloat(RadiusAttribute.Name, out float radius))
            {
                return false;
            }

            Radius = radius;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return InPlane(local, out _).SquaredMagnitude <= MathHelpers.SquaredReach(Radius, ToleranceInPlane);
        }

        /// <inheritdoc />
        /// <remarks>A random point lies in the disc's own plane, since the disc ignores height.</remarks>
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            local = OutOfPlane(MathHelpers.RandomInDisc(RandomSource, Radius), 0f);

            return true;
        }
    }
}
