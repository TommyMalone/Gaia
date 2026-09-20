using System;

namespace Gaia.Core.Services.Random
{
    /// <summary>
    ///     A random source that always returns the same value, so a <see cref="Reasoners.DualUtilityReasoner" />
    ///     selection or any other roll is deterministic.
    /// </summary>
    public sealed class FixedRandomSource : IRandomSource
    {
        private readonly float _value;

        /// <summary>Creates a source that always returns 0.</summary>
        public FixedRandomSource() : this(0f)
        {
        }

        /// <summary>Creates a source that answers every ask with the given value.</summary>
        /// <param name="value">
        ///     The value NextFloat returns. Should be in the range [0, 1), the range
        ///     <see cref="IRandomSource.NextFloat" /> promises. The bounded NextInt overloads return it truncated to an int and
        ///     clamped into their range, and the parameterless NextInt returns it truncated, so a value in [0, 1) makes every one
        ///     of them return its lower bound.
        /// </param>
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
            if (maxExclusive < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive,
                    "The upper bound is negative.");
            }

            return NextInt(0, maxExclusive);
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive > maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive), minInclusive,
                    "The lower bound is above the upper bound.");
            }

            if (minInclusive == maxExclusive)
            {
                return minInclusive;
            }

            return Math.Clamp((int)_value, minInclusive, maxExclusive - 1);
        }
    }
}
