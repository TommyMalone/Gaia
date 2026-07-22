namespace Gaia.Core.Services
{
    /// <summary>A random source that always returns the same value, which makes weighted selection deterministic.</summary>
    public sealed class FixedRandomSource : IRandomSource
    {
        private readonly float _value;

        /// <summary>Creates a source that always returns 0.</summary>
        public FixedRandomSource() : this(0f)
        {
        }

        /// <param name="value">The value to return. Should be in the range [0, 1) to match a real random source.</param>
        public FixedRandomSource(float value)
        {
            _value = value;
        }

        /// <inheritdoc />
        public float NextFloat()
        {
            return _value;
        }

        /// <inheritdoc />
        public int NextInt()
        {
            return (int)_value;
        }

        /// <inheritdoc />
        public int NextInt(int maxExclusive)
        {
            return (int)_value;
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return minInclusive;
        }
    }
}
