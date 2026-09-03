using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape in the ground plane, which ignores height.</summary>
    /// <remarks>A random position is reported at the Y of Corners[0].</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class PolygonRegion : PolygonRegionBase
    {
        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return IsInFootprint(position);
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!HasArea)
            {
                position = Vector3.Zero;

                return false;
            }

            float height = Corners[0].Y;

            Vector3 min = new(BoundsMin.X, height, BoundsMin.Z);
            Vector3 max = new(BoundsMax.X, height, BoundsMax.Z);

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }
    }
}
