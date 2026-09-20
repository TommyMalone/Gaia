using System;
using System.Threading;
using Gaia.Core.Strings;

namespace Gaia.Core.Services.Random
{
    /// <summary>
    ///     The default random source, a xoshiro128** generator. Construct with a seed to reproduce a run, or without one
    ///     to take a seed from the clock.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The seed is kept in <see cref="Seed" /> either way, so a host that reports it can reproduce a run by
    ///         constructing another source with the reported value.
    ///     </para>
    ///     <para>
    ///         The generator is implemented here rather than wrapping System.Random because that class documents its
    ///         algorithm as unspecified, and a seed reported on one runtime could then produce a different sequence on
    ///         another. The same seed produces the same sequence on every runtime this class compiles for.
    ///     </para>
    ///     <para>
    ///         A reported seed reproduces a run only while every component draws in the same order, so a change that adds,
    ///         removes or reorders draws on the decision path shifts every value drawn from the same source after it.
    ///     </para>
    /// </remarks>
    /// <seealso>David Blackman and Sebastiano Vigna, xoshiro128**, described at https://prng.di.unimi.it</seealso>
    public sealed class Xoshiro128RandomSource : IRandomSource
    {
        /// <summary>
        ///     Separates default seeds taken at the same Environment.TickCount reading, so a burst of sources never
        ///     correlates.
        /// </summary>
        private static int _defaultSeedCount;

        private uint _state0;
        private uint _state1;
        private uint _state2;
        private uint _state3;

        /// <summary>Creates a source seeded from the clock.</summary>
        /// <remarks>
        ///     The Environment.TickCount reading is combined with a count of default constructions, because a loop that makes
        ///     several default sources inside one millisecond would otherwise hand them all the same seed and the sources would
        ///     move in step. The count is multiplied by 0x2545F491 first, so consecutive counts differ across the whole word
        ///     rather than only in the low bits a clock reading also varies.
        /// </remarks>
        public Xoshiro128RandomSource() : this(Environment.TickCount ^
                                               (Interlocked.Increment(ref _defaultSeedCount) * 0x2545F491))
        {
        }

        /// <summary>Creates a source that runs from the given seed.</summary>
        /// <param name="seed">A seed value. The same seed produces the same sequence of values.</param>
        public Xoshiro128RandomSource(int seed)
        {
            Seed = seed;

            // The four state words come from a splitmix64 walk over the seed, the seeding the generator's authors
            // recommend. The walk's increment keeps the state from ever starting all zero, the one state the
            // generator cannot leave.
            ulong seedingState = (uint)seed;

            ulong first = SplitMix64(ref seedingState);
            ulong second = SplitMix64(ref seedingState);

            _state0 = (uint)first;
            _state1 = (uint)(first >> 32);
            _state2 = (uint)second;
            _state3 = (uint)(second >> 32);
        }

        /// <summary>The seed the sequence runs from, which is what reproduces it.</summary>
        public int Seed { get; }

        /// <inheritdoc />
        public float NextFloat()
        {
            // A float has a 24-bit mantissa, so every multiple of 1/2^24 below 1 is exactly representable. Dividing
            // the draw's top 24 bits by 2^24 therefore yields at most 1 - 2^-24, with no rounding.
            const float precision = 1 << 24;

            return (Next32() >> 8) / precision;
        }

        /// <inheritdoc />
        public int NextInt()
        {
            int result;

            // A result of int.MaxValue is redrawn, because IRandomSource.NextInt promises a value below it.
            do
            {
                result = (int)(Next32() >> 1);
            } while (result == int.MaxValue);

            return result;
        }

        /// <inheritdoc />
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive,
                    "The upper bound is negative.");
            }

            return (int)NextBounded((uint)maxExclusive);
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive > maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive), minInclusive,
                    "The lower bound is above the upper bound.");
            }

            return (int)(minInclusive + NextBounded((uint)((long)maxExclusive - minInclusive)));
        }

        /// <summary>Produces a value at or above 0 and below the bound, exactly uniformly. A bound of 0 returns 0.</summary>
        /// <remarks>
        ///     The threshold is 2^32 mod the bound. A 32-bit value below it is rejected and redrawn, so every value below the
        ///     bound is produced by the same number of accepted 32-bit values and none is favored. The redraw count depends on the
        ///     drawn values, so how many values a bounded draw takes varies, and every draw that follows from the source depends
        ///     on it.
        /// </remarks>
        private long NextBounded(uint boundExclusive)
        {
            if (boundExclusive == 0)
            {
                return 0;
            }

            uint threshold = (uint)-boundExclusive % boundExclusive;
            uint draw;

            do
            {
                draw = Next32();
            } while (draw < threshold);

            return draw % boundExclusive;
        }

        /// <summary>Advances the generator one step and produces its next 32 bits.</summary>
        private uint Next32()
        {
            uint result = RotateLeft(_state1 * 5, 7) * 9;
            uint shifted = _state1 << 9;

            _state2 ^= _state0;
            _state3 ^= _state1;
            _state1 ^= _state2;
            _state0 ^= _state3;
            _state2 ^= shifted;
            _state3 = RotateLeft(_state3, 11);

            return result;
        }

        /// <summary>Derives a stream seed from a master seed and a name, the same pair always giving the same seed.</summary>
        /// <remarks>
        ///     One splitmix64 step over the master seed combined with the name's hash. <see cref="Factories.GaiaLoader" />
        ///     seeds a named actor's brain this way, so the brain keeps its stream when a content edit shifts the load order. Like
        ///     the generator, the mix and each name's AiString.Hash must stay unchanged for a reported seed to reproduce a run.
        /// </remarks>
        internal static int DeriveSeed(int masterSeed, AiString name)
        {
            ulong state = (uint)masterSeed ^ name.Hash;

            return unchecked((int)SplitMix64(ref state));
        }

        /// <summary>
        ///     One step of the splitmix64 sequence, used to spread the seed across the state words and, in DeriveSeed, to mix
        ///     a master seed with a name.
        /// </summary>
        private static ulong SplitMix64(ref ulong state)
        {
            ulong mixed = state += 0x9E3779B97F4A7C15;

            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EB;

            return mixed ^ (mixed >> 31);
        }

        /// <summary>Rotates the bits of the value left by the count, the bits shifted out coming back in at the bottom.</summary>
        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }
    }
}
