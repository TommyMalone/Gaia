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
            AttributeKind.Float, "How far the shape reaches either side of its plane.");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>How far the region reaches either side of its plane.</summary>
        public float HalfHeight { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!LoadFrame(creationData) ||
                !creationData.Node.ReadFloat(HalfHeightAttribute.Name, out float halfHeight))
            {
                return false;
            }

            HalfHeight = halfHeight;
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            Vector2 point = Frame.ToLocal(position, out float height);

            return MathF.Abs(height) <= HalfHeight && IsInFootprint(point);
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetRandomFootprintPoint(out Vector2 point))
            {
                position = Vector3.Zero;

                return false;
            }

            position = Frame.ToWorld(point, MathHelpers.RandomInRange(RandomSource, -HalfHeight, HalfHeight));

            return true;
        }
    }
}
