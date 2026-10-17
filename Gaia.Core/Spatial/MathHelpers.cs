using System;
using Gaia.Core.Services.Random;

namespace Gaia.Core.Spatial
{
    /// <summary>The arithmetic the library shares, its angle conversions, the round shapes' reach and the random picks.</summary>
    internal static class MathHelpers
    {
        /// <summary>Multiplies an angle in degrees to give it in radians.</summary>
        internal const float DegreesToRadians = MathF.PI / 180f;

        /// <summary>Multiplies an angle in radians to give it in degrees.</summary>
        internal const float RadiansToDegrees = 180f / MathF.PI;

        /// <summary>How many guesses <see cref="Regions.RegionBase.TryRejectionSample" /> makes before giving up.</summary>
        /// <remarks>
        ///     A sphere fills just over half its bounding box, and a polygon or a hull usually fills more of its bounds than
        ///     that, so this many misses in a row is unlikely for any shape worth configuring. A long thin diagonal sliver is the
        ///     kind of shape that exhausts it.
        /// </remarks>
        internal const int MaxSampleAttempts = 32;

        /// <summary>
        ///     The square of how far a round shape reaches from its middle or its axis, its radius with its tolerance added,
        ///     for the round shapes to compare a squared distance with.
        /// </summary>
        /// <remarks>
        ///     The tolerance is the shape's own, in its own units, across the axes its distance mixes. It is added before
        ///     squaring, so that it stays a length. The radius's size is taken first, so a negative radius reaches as far as a
        ///     positive one does.
        /// </remarks>
        internal static float SquaredReach(float radius, float tolerance)
        {
            float reach = MathF.Abs(radius) + tolerance;

            return reach * reach;
        }

        /// <summary>Picks a value uniformly between two bounds.</summary>
        internal static float RandomInRange(IRandomSource randomSource, float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * randomSource.NextFloat();
        }

        /// <summary>Picks a point uniformly inside a disc of the given radius around (0, 0).</summary>
        /// <remarks>
        ///     The square root keeps the density even. Without it, points would crowd toward the middle, since a ring near
        ///     the edge has far more area than a ring of the same width near the middle.
        /// </remarks>
        /// <seealso>Disk point picking, described at https://mathworld.wolfram.com/DiskPointPicking.html</seealso>
        internal static Vector2 RandomInDisc(IRandomSource randomSource, float radius)
        {
            float distance = radius * MathF.Sqrt(randomSource.NextFloat());
            float angle = randomSource.NextFloat() * 2f * MathF.PI;

            return new Vector2(distance * MathF.Cos(angle), distance * MathF.Sin(angle));
        }

        /// <summary>Picks a position uniformly inside an axis-aligned box.</summary>
        internal static Vector3 RandomInBox(IRandomSource randomSource, Vector3 min, Vector3 max)
        {
            return new Vector3(RandomInRange(randomSource, min.X, max.X), RandomInRange(randomSource, min.Y, max.Y),
                RandomInRange(randomSource, min.Z, max.Z));
        }
    }
}
