using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A disc extruded a half-height either side of its plane, which makes a cylinder.</summary>
    /// <remarks>
    ///     <para>Origin is the middle of the cylinder. Unturned, the cylinder stands upright.</para>
    ///     <para>
    ///         A random point is picked on the disc the way <see cref="CircleRegion" /> picks one and spread evenly along
    ///         the height, so the cylinder's own pick never fails.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CylinderRegion : PlanarRegionBase
    {
        private static readonly DescriptionBuilder Builder = DescribeShape("An upright cylinder around its Origin.");

        private static readonly AttributeDescription RadiusAttribute = Builder.Required("Radius", AttributeKind.Float,
            "How far the cylinder reaches from its axis.");

        private static readonly AttributeDescription HalfHeightAttribute = Builder.Required("HalfHeight",
            AttributeKind.Float,
            "How far the cylinder reaches either side of its plane, which runs through Origin at its middle. A negative value is read as its size.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private float _halfHeight;

        /// <summary>How far the region reaches from its axis.</summary>
        public float Radius { get; set; }

        /// <summary>How far the region reaches either side of its plane.</summary>
        /// <remarks>A negative value is stored as its size, so -1.5 reads back as 1.5.</remarks>
        public float HalfHeight
        {
            get => _halfHeight;
            set => _halfHeight = MathF.Abs(value);
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
            Vector2 point = InPlane(local, out float height);

            // Height is local Y, so the half height takes the tolerance along it.
            return MathF.Abs(height) <= HalfHeight + ToleranceAlongAxes.Y &&
                   point.SquaredMagnitude <= MathHelpers.SquaredReach(Radius, ToleranceInPlane);
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector2 point = MathHelpers.RandomInDisc(RandomSource, Radius);
            float height = MathHelpers.RandomInRange(RandomSource, -HalfHeight, HalfHeight);

            local = OutOfPlane(point, height);

            return true;
        }
    }
}
