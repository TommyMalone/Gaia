using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape extruded a half-height either side of its plane.</summary>
    /// <remarks>
    ///     <para>
    ///         It is the only shape here that is both concave and bounded in height, which is what most level geometry is. A
    ///         room is a footprint with a floor and a ceiling, and so is the inside of a building, with Origin halfway between
    ///         the two.
    ///     </para>
    ///     <para>A random position is spread evenly along the height.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class ExtrudedPolygonRegion : PolygonRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeFootprint("A footprint of any shape given a height.");

        private static readonly AttributeDescription HalfHeightAttribute = Builder.Required("HalfHeight",
            AttributeKind.Float,
            "How far the shape reaches either side of its plane. A negative value is read as its size.");

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

            return MathF.Abs(height) <= HalfHeight && IsInFootprint(point);
        }

        /// <inheritdoc />
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
