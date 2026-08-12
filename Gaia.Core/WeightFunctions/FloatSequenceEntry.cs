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
        /// <param name="thresholdMin">The lowest threshold this entry can roll.</param>
        /// <param name="thresholdMax">The highest threshold this entry can roll.</param>
        /// <param name="weights">The values to report for an input this entry covers.</param>
        internal FloatSequenceEntry(float thresholdMin, float thresholdMax, WeightValues weights)
        {
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

        /// <summary>True if RollThreshold rolls a new Threshold when its option is selected.</summary>
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
