using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape in its own plane, which ignores height.</summary>
    /// <remarks>A random position lies in the footprint's own plane.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class PolygonRegion : PolygonRegionBase
    {
        internal static readonly ComponentDescription Description =
            DescribeFootprint("A footprint of any shape on the ground, which ignores height.").Build();

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return IsInFootprint(InPlane(local, out _));
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            if (!TryGetRandomFootprintPoint(out Vector2 point))
            {
                local = Vector3.Zero;

                return false;
            }

            local = OutOfPlane(point, 0f);

            return true;
        }
    }
}
