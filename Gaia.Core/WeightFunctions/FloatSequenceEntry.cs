using System;
using Gaia.Core.Services.Random;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     One threshold in a <see cref="FloatSequenceWeightFunction" />, along with the weight values to report for
    ///     inputs at or below it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The threshold is a single value rather than a band. An entry covers everything from the previous entry's
    ///         threshold up to its own.
    ///     </para>
    ///     <para>
    ///         Most entries hold a fixed threshold. An entry given a ThresholdMin below its ThresholdMax rolls Threshold
    ///         from between them as it is added to a function, and again each time the <see cref="Options.OptionBase" />
    ///         holding the function is selected while RerollOnSelect holds. Threshold is ThresholdMin until the entry is
    ///         added.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class FloatSequenceEntry
    {
        /// <summary>Creates an entry whose threshold never changes.</summary>
        /// <param name="threshold">The highest input this entry covers.</param>
        /// <param name="weights">The values to report for an input this entry covers.</param>
        internal FloatSequenceEntry(float threshold, WeightValues weights) : this(threshold, threshold, weights)
        {
        }

        /// <summary>Creates an entry whose threshold is rolled between the two values, or fixed where they are equal.</summary>
        /// <param name="thresholdMin">The lowest threshold this entry can roll.</param>
        /// <param name="thresholdMax">The highest threshold this entry can roll.</param>
        /// <param name="weights">The values to report for an input this entry covers.</param>
        /// <exception cref="ArgumentException">thresholdMax is below thresholdMin.</exception>
        internal FloatSequenceEntry(float thresholdMin, float thresholdMax, WeightValues weights)
        {
            if (thresholdMax < thresholdMin)
            {
                throw new ArgumentException(
                    $"A float sequence entry cannot roll between {thresholdMin} and {thresholdMax}, since the maximum is below the minimum.",
                    nameof(thresholdMax));
            }

            ThresholdMin = thresholdMin;
            ThresholdMax = thresholdMax;
            Weights = weights;

            Threshold = thresholdMin;
        }

        /// <summary>The lowest value Threshold can take.</summary>
        public float ThresholdMin { get; internal set; }

        /// <summary>The highest value Threshold can take. Equal to ThresholdMin for an entry that never changes.</summary>
        public float ThresholdMax { get; internal set; }

        /// <summary>The weight values to report for an input this entry covers.</summary>
        public WeightValues Weights { get; internal set; }

        /// <summary>The threshold currently in effect.</summary>
        public float Threshold { get; private set; }

        /// <summary>Whether a randomized entry rolls again each time its option is selected, rather than once.</summary>
        /// <remarks>
        ///     True unless a configuration turns it off, so an entry that rolls keeps the chapter's behavior of a fresh value
        ///     per selection. An entry that holds its first roll is one whose value is meant to vary between characters rather
        ///     than between runs of the same character. A fixed entry has nothing to reroll, so the flag has no effect on one and
        ///     a configuration that sets it there is refused.
        /// </remarks>
        public bool RerollOnSelect { get; internal set; } = true;

        /// <summary>True if this entry rolls its Threshold rather than holding the value it was given.</summary>
        public bool IsRandomized => ThresholdMax > ThresholdMin;

        /// <summary>Rolls a new Threshold between ThresholdMin and ThresholdMax. Does nothing unless IsRandomized.</summary>
        internal void RollThreshold(IRandomSource randomSource)
        {
            if (!IsRandomized)
            {
                return;
            }

            Threshold = ThresholdMin + randomSource.NextFloat() * (ThresholdMax - ThresholdMin);
        }
    }
}
