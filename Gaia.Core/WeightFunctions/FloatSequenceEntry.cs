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
    ///         Most entries hold a fixed threshold. An entry given a ThresholdMinimum below its ThresholdMaximum rolls Threshold
    ///         from between them in RollThreshold each time the containing option is selected.
    ///     </para>
    /// </remarks>
    public class FloatSequenceEntry
    {
        /// <summary>Creates an entry whose threshold never changes.</summary>
        /// <param name="threshold">The highest input this entry covers.</param>
        /// <param name="weights">The values to report for an input this entry covers.</param>
        internal FloatSequenceEntry(float threshold, WeightValues weights) : this(threshold, threshold, weights)
        {
        }

        /// <summary>Creates an entry whose threshold is rolled between a range each time its option is selected.</summary>
        /// <param name="thresholdMinimum">The lowest threshold this entry can roll.</param>
        /// <param name="thresholdMaximum">The highest threshold this entry can roll.</param>
        /// <param name="weights">The values to report for an input this entry covers.</param>
        internal FloatSequenceEntry(float thresholdMinimum, float thresholdMaximum, WeightValues weights)
        {
            ThresholdMinimum = thresholdMinimum;
            ThresholdMaximum = thresholdMaximum;
            Weights = weights;

            Threshold = thresholdMinimum;
        }

        /// <summary>The lowest value Threshold can take.</summary>
        public float ThresholdMinimum { get; internal set; }

        /// <summary>The highest value Threshold can take. Equal to ThresholdMinimum for an entry that never changes.</summary>
        public float ThresholdMaximum { get; internal set; }

        /// <summary>The weight values to report for an input this entry covers.</summary>
        public WeightValues Weights { get; internal set; }

        /// <summary>The threshold currently in effect.</summary>
        public float Threshold { get; private set; }

        /// <summary>True if RollThreshold rolls a new Threshold when its option is selected.</summary>
        public bool IsRandomized => ThresholdMaximum > ThresholdMinimum;

        /// <summary>Rolls a new Threshold between ThresholdMinimum and ThresholdMaximum. Does nothing unless IsRandomized.</summary>
        internal void RollThreshold(IRandomSource randomSource)
        {
            if (!IsRandomized)
            {
                return;
            }

            Threshold = ThresholdMinimum + randomSource.NextFloat() * (ThresholdMaximum - ThresholdMinimum);
        }
    }
}
