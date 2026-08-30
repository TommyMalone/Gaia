using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>An axis-aligned box, which takes height into account.</summary>
    /// <remarks>
    ///     <see cref="RectangleRegion" /> with the Y axis included. TryGetRandomPosition never fails, since every point
    ///     of the box is inside it.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class BoxRegion : RegionBase
    {
        /// <summary>Creates a box whose corners are set through Min and Max.</summary>
        public BoxRegion()
        {
        }

        /// <summary>Creates a box reaching the given extents out from a center.</summary>
        /// <param name="center">Where the middle of the box is.</param>
        /// <param name="extents">How far the box reaches from center along each axis, half of its size in each.</param>
        internal BoxRegion(Vector3 center, Vector3 extents)
        {
            Min = center - extents;
            Max = center + extents;
        }

        /// <summary>The corner with the smallest X, Y and Z.</summary>
        public Vector3 Min { get; internal set; }

        /// <summary>The corner with the largest X, Y and Z.</summary>
        public Vector3 Max { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X && position.Y >= Min.Y && position.Y <= Max.Y &&
                   position.Z >= Min.Z && position.Z <= Max.Z;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            position = MathHelpers.RandomInBox(RandomSource, Min, Max);

            return true;
        }
    }
}
