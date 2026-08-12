using System.Collections.Generic;
using Gaia.Core.Services.Random;
using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Divides the possible inputs into sections and reports a different set of weight values for each.</summary>
    /// <remarks>
    ///     The first entry whose threshold the input falls at or below is the one that answers, and an input above every
    ///     threshold gets the default. Entries are expected in ascending order of threshold, since that is the order they are
    ///     tested in. Configuring entries at 50 and 300 with a vetoing default, for example, expresses "only targets between
    ///     50 and 300 meters away".
    /// </remarks>
    public class FloatSequenceWeightFunction : WeightFunctionBase
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

        /// <summary>Add an entry. Entries must be added in ascending order of threshold.</summary>
        internal void AddEntry(FloatSequenceEntry entry)
        {
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
