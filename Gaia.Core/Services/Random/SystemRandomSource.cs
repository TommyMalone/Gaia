using System;
using System.Threading;

namespace Gaia.Core.Services.Random
{
    /// <summary>
    ///     The default random source, backed by System.Random. Construct with a seed to reproduce a run, or without one
    ///     to take a seed from the clock.
    /// </summary>
    /// <remarks>
    ///     The seed is kept in <see cref="Seed" /> either way, so a host that reports it can reproduce a run by
    ///     constructing another source with the reported value.
    /// </remarks>
    public sealed class SystemRandomSource : IRandomSource
    {
        /// <summary>
        ///     Separates default seeds taken at the same Environment.TickCount reading, so a burst of sources never
        ///     correlates.
        /// </summary>
        private static int _defaultSeedCount;

        private readonly System.Random _random;

        /// <summary>Creates a source seeded from the clock.</summary>
        /// <remarks>
        ///     The Environment.TickCount reading is combined with a count of default constructions, because a loop that makes
        ///     several default sources inside one millisecond would otherwise hand them all the same seed and the sources would
        ///     move in step. The count is multiplied by 0x2545F491 first, so consecutive counts differ across the whole word
        ///     rather than only in the low bits a clock reading also varies.
        /// </remarks>
        public SystemRandomSource() : this(Environment.TickCount ^
                                           (Interlocked.Increment(ref _defaultSeedCount) * 0x2545F491))
        {
        }

        /// <param name="seed">A seed value. The same seed produces the same sequence of values.</param>
        public SystemRandomSource(int seed)
        {
            Seed = seed;
            _random = new System.Random(seed);
        }

        /// <summary>The seed the sequence runs from, which is what reproduces it.</summary>
        public int Seed { get; }

        /// <inheritdoc />
        public float NextFloat()
        {
            // A float has a 24-bit mantissa, so every multiple of 1/2^24 below 1 is exactly representable. Dividing an
            // integer drawn from [0, 2^24) by 2^24 therefore yields at most 1 - 2^-24, with no rounding. Casting
            // NextDouble() to float instead can round upward to exactly 1 when the double is close enough.
            const int precision = 1 << 24;

            return (float)_random.Next(precision) / precision;
        }

        /// <inheritdoc />
        public int NextInt()
        {
            return _random.Next();
        }

        /// <inheritdoc />
        public int NextInt(int maxExclusive)
        {
            return _random.Next(maxExclusive);
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
