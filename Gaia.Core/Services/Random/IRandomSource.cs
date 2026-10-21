using System;

namespace Gaia.Core.Services.Random
{
    /// <summary>Supplies the random values needed throughout Gaia. A roll is one value taken from the source.</summary>
    /// <remarks>The source is injected rather than accessed statically, so that tests can substitute a deterministic source.</remarks>
    /// <seealso>"Modular AI", Section 8.4.4.6</seealso>
    public interface IRandomSource
    {
        /// <summary>Produces a random floating-point value.</summary>
        /// <returns>A value at or above 0 and below 1.</returns>
        float NextFloat();

        /// <summary>Produces a random non-negative integer.</summary>
        /// <returns>A value at or above 0 and below int.MaxValue.</returns>
        int NextInt();

        /// <summary>Produces a random non-negative integer below the given bound.</summary>
        /// <param name="maxExclusive">
        ///     The upper bound. Must not be negative. A bound above 0 is never returned, and a bound of 0
        ///     returns 0.
        /// </param>
        /// <returns>A value at or above 0 and below maxExclusive.</returns>
        /// <exception cref="ArgumentOutOfRangeException">maxExclusive is negative.</exception>
        int NextInt(int maxExclusive);

        /// <summary>Produces a random integer within the given range.</summary>
        /// <param name="minInclusive">The lower bound, which may be returned.</param>
        /// <param name="maxExclusive">
        ///     The upper bound. Must not be below minInclusive. A bound above minInclusive is never
        ///     returned, and equal bounds return that value.
        /// </param>
        /// <returns>A value at or above minInclusive and below maxExclusive.</returns>
        /// <exception cref="ArgumentOutOfRangeException">minInclusive is above maxExclusive.</exception>
        int NextInt(int minInclusive, int maxExclusive);
    }
}
