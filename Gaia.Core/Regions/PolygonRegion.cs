using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape in the plane of its frame, which ignores height.</summary>
    /// <remarks>A random position lies in the footprint's own plane.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class PolygonRegion : PolygonRegionBase
    {
        internal static readonly ComponentDescription Description =
            DescribeFootprint("A footprint of any shape on the ground, which ignores height.").Build();

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            return LoadFrame(creationData) && LoadFootprint(creationData);
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return IsInFootprint(Frame.ToLocal(position, out _));
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetRandomFootprintPoint(out Vector2 point))
            {
                position = Vector3.Zero;

                return false;
            }

            position = Frame.ToWorld(point, 0f);

            return true;
        }
    }
}
