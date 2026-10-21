using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape extruded a half-height either side of its plane.</summary>
    /// <remarks>A room is a footprint of any shape with a floor and a ceiling, and Origin sits halfway between the two.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class ExtrudedPolygonRegion : PolygonRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeFootprint("A footprint of any shape given a height, with its Origin halfway up.");

        private static readonly AttributeDescription HalfHeightAttribute = Builder.Required("HalfHeight",
            AttributeKind.Float,
            "How far the shape reaches either side of its plane, which runs through Origin halfway up, so a room 3 high on a floor at 0 has Origin y 1.5 and HalfHeight 1.5. A negative value is read as its size.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private float _halfHeight;

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
            if (!creationData.Node.ReadFloat(HalfHeightAttribute.Name, out float halfHeight))
            {
                return false;
            }

            HalfHeight = halfHeight;
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            Vector2 point = InPlane(local, out float height);

            // Height is local Y, so the half height takes the tolerance along it.
            return MathF.Abs(height) <= HalfHeight + ToleranceAlongAxes.Y && IsInFootprint(point);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     The point is a random point of the footprint spread evenly along the height, so the pick fails only when the
        ///     footprint's does.
        /// </remarks>
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            if (!TryGetRandomFootprintPoint(out Vector2 point))
            {
                local = Vector3.Zero;

                return false;
            }

            local = OutOfPlane(point, MathHelpers.RandomInRange(RandomSource, -HalfHeight, HalfHeight));

            return true;
        }
    }
}
