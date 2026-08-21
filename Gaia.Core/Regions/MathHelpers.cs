using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>The arithmetic the region shapes share.</summary>
    internal static class MathHelpers
    {
        /// <summary>How many guesses TryRejectionSample makes before giving up.</summary>
        /// <remarks>
        ///     A sphere fills just over half its bounding box, and a polygon or a hull usually fills more of its bounds than
        ///     that, so this many misses in a row is unlikely for any shape worth configuring. A long thin diagonal sliver is the
        ///     kind of shape that exhausts it.
        /// </remarks>
        internal const int MaxSampleAttempts = 32;

        /// <summary>Picks a value uniformly between two bounds.</summary>
        internal static float RandomInRange(IRandomSource randomSource, float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * randomSource.NextFloat();
        }

        /// <summary>Picks a position uniformly inside an axis-aligned box.</summary>
        internal static Vector3 RandomInBox(IRandomSource randomSource, Vector3 min, Vector3 max)
        {
            return new Vector3(RandomInRange(randomSource, min.X, max.X), RandomInRange(randomSource, min.Y, max.Y),
                RandomInRange(randomSource, min.Z, max.Z));
        }

        /// <summary>Guesses positions inside a box until one is inside the region, or the attempts run out.</summary>
        /// <param name="region">The shape to test each guess against.</param>
        /// <param name="min">The lowest corner of the box to guess inside.</param>
        /// <param name="max">The highest corner of the box to guess inside.</param>
        /// <param name="position">A position inside the region, if one was found.</param>
        /// <returns>True if a guess landed inside the region.</returns>
        internal static bool TryRejectionSample(RegionBase region, Vector3 min, Vector3 max, out Vector3 position)
        {
            for (int attempt = 0; attempt < MaxSampleAttempts; attempt++)
            {
                position = RandomInBox(region.RandomSource, min, max);

                if (region.IsInRegion(position))
                {
                    return true;
                }
            }

            position = Vector3.Zero;

            return false;
        }
    }
}
