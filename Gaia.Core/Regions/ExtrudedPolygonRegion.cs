using Gaia.Core.Spatial;
using Gaia.Core.Temp;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape between a floor and a ceiling.</summary>
    /// <remarks>
    ///     <para>
    ///         It is the only shape here that is both concave and bounded in height, which is what most level geometry is. A
    ///         room is a footprint with a floor and a ceiling, and so is the inside of a building.
    ///     </para>
    ///     <para>A random position is spread evenly between Floor and Ceiling.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class ExtrudedPolygonRegion : PolygonRegionBase
    {
        /// <summary>The lowest height inside the region.</summary>
        public float Floor { get; internal set; }

        /// <summary>The highest height inside the region.</summary>
        public float Ceiling { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return position.Y >= Floor && position.Y <= Ceiling && IsInFootprint(position);
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!HasArea)
            {
                position = Vector3.Zero;

                return false;
            }

            Vector3 min = new(BoundsMin.X, Floor, BoundsMin.Z);
            Vector3 max = new(BoundsMax.X, Ceiling, BoundsMax.Z);

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }
    }
}
