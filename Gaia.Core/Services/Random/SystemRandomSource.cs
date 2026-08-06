namespace Gaia.Core.Services.Random
{
    /// <summary>
    ///     The default random source, backed by System.Random. Construct with a seed to reproduce a run, or without one
    ///     to seed from the clock.
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _random;

        /// <summary>Creates a source seeded from the clock.</summary>
        public SystemRandomSource()
        {
            _random = new System.Random();
        }

        /// <param name="seed">A seed value. The same seed produces the same sequence of values.</param>
        public SystemRandomSource(int seed)
        {
            _random = new System.Random(seed);
        }

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
