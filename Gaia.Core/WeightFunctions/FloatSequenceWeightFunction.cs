using System;
using System.Collections.Generic;
using Gaia.Core.Services.Random;
using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Divides the possible inputs into sections and reports a different set of weight values for each.</summary>
    /// <remarks>
    ///     <para>
    ///         The first entry whose Threshold the input falls at or below is the one that answers, and an input above every
    ///         threshold gets DefaultWeights. Entries are expected in ascending order of Threshold, since that is the order
    ///         they are tested in, and AddEntry rejects one that is out of order.
    ///     </para>
    ///     <para>
    ///         Configuring a vetoing entry at 50, an allowing entry at 300 and a vetoing default, for example, expresses
    ///         "only targets between 50 and 300 meters away".
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class FloatSequenceWeightFunction : WeightFunctionBase
    {
        private readonly List<FloatSequenceEntry> _entries = new();

        /// <summary>Creates a function with no entries that reports the given values for every input.</summary>
        internal FloatSequenceWeightFunction(WeightValues defaultWeights)
        {
            DefaultWeights = defaultWeights;
        }

        /// <summary>The weight values to report for an input above every entry's threshold.</summary>
        public WeightValues DefaultWeights { get; internal set; }

        /// <summary>
        ///     Only used by entries whose <see cref="FloatSequenceEntry.IsRandomized" /> is true, in
        ///     <see cref="FloatSequenceEntry.RollThreshold" />.
        /// </summary>
        public IRandomSource RandomSource { get; internal set; } = new SystemRandomSource();

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Adds an entry. Entries must be added in ascending order of <see cref="FloatSequenceEntry.Threshold" />.</summary>
        /// <remarks>
        ///     An entry that rolls its threshold has to stay in order whatever it rolls, so what is compared is the new
        ///     entry's <see cref="FloatSequenceEntry.ThresholdMin" /> against the previous entry's
        ///     <see cref="FloatSequenceEntry.ThresholdMax" />. Two ranges that overlap would swap places on some rolls and not
        ///     others, and a configuration that does that is never what was meant.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     The entry's <see cref="FloatSequenceEntry.ThresholdMin" /> is below the
        ///     <see cref="FloatSequenceEntry.ThresholdMax" /> of the entry before it.
        /// </exception>
        internal void AddEntry(FloatSequenceEntry entry)
        {
            if (_entries.Count > 0)
            {
                FloatSequenceEntry previousEntry = _entries[_entries.Count - 1];

                if (entry.ThresholdMin < previousEntry.ThresholdMax)
                {
                    throw new ArgumentException(
                        $"A float sequence entry starting at {entry.ThresholdMin} cannot follow one reaching " +
                        $"{previousEntry.ThresholdMax}. Entries must be added in ascending order and must not overlap.",
                        nameof(entry));
                }
            }

            _entries.Add(entry);
        }

        /// <inheritdoc />
        public override WeightValues CalculateFloat(float value)
        {
            foreach (FloatSequenceEntry entry in _entries)
            {
                if (value <= entry.Threshold)
                {
                    return entry.Weights;
                }
            }

            return DefaultWeights;
        }

        /// <summary>
        ///     Calls <see cref="FloatSequenceEntry.RollThreshold" /> on every entry, which rolls a fresh threshold for any
        ///     entry that is randomized. Entries with a fixed threshold are the common case and are left alone.
        /// </summary>
        public override void Select()
        {
            foreach (FloatSequenceEntry entry in _entries)
            {
                entry.RollThreshold(RandomSource);
            }
        }
    }
}
