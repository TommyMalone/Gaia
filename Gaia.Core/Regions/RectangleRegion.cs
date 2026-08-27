using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>An axis-aligned rectangle in the ground plane, which ignores height.</summary>
    /// <remarks>Only X and Z of Min and Max are used by IsInRegion. A random position is reported at Min.Y.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class RectangleRegion : RegionBase
    {
        /// <summary>The corner with the smallest X and Z.</summary>
        public Vector3 Min { get; internal set; }

        /// <summary>The corner with the largest X and Z.</summary>
        public Vector3 Max { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X && position.Z >= Min.Z && position.Z <= Max.Z;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            position = new Vector3(MathHelpers.RandomInRange(RandomSource, Min.X, Max.X), Min.Y,
                MathHelpers.RandomInRange(RandomSource, Min.Z, Max.Z));

            return true;
        }
    }
}
