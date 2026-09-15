using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Services.Random;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

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
        private static readonly AiString DefaultElement = "Default";
        private static readonly AiString EntryElement = "Entry";
        private static readonly AiString ThresholdAttribute = "Threshold";
        private static readonly AiString ThresholdMaxAttribute = "ThresholdMax";
        private static readonly AiString ThresholdMinAttribute = "ThresholdMin";

        private readonly List<FloatSequenceEntry> _entries = new();

        /// <summary>Creates a function with no entries that reports Neutral for every input until a configuration loads.</summary>
        internal FloatSequenceWeightFunction() : this(WeightValues.Neutral)
        {
        }

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
        public IRandomSource RandomSource { get; internal set; } = new Xoshiro128RandomSource();

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            RandomSource = creationData.RandomSource;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == DefaultElement)
                {
                    if (!WeightValues.TryRead(child, out WeightValues defaultWeights))
                    {
                        return false;
                    }

                    DefaultWeights = defaultWeights;
                }
                else if (child.Name == EntryElement)
                {
                    if (!TryLoadEntry(child, creationData))
                    {
                        return false;
                    }
                }
                else
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }
            }

            return true;
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
        protected override void OnSelect()
        {
            foreach (FloatSequenceEntry entry in _entries)
            {
                entry.RollThreshold(RandomSource);
            }
        }

        /// <summary>Loads one entry from an Entry element, in either its fixed or its rolled threshold form.</summary>
        /// <remarks>
        ///     The checks <see cref="AddEntry" /> and the entry's constructor enforce with exceptions are made here first and
        ///     reported through the sink instead, so a configuration mistake reads as a located error rather than a thrown
        ///     exception.
        /// </remarks>
        private bool TryLoadEntry(SpecificationNode entryNode, AiCreationData creationData)
        {
            float thresholdMin;
            float thresholdMax;

            bool hasRange = entryNode.HasAttribute(ThresholdMinAttribute) ||
                            entryNode.HasAttribute(ThresholdMaxAttribute);

            if (hasRange && entryNode.HasAttribute(ThresholdAttribute))
            {
                creationData.ReportError(
                    $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element mixes Threshold with ThresholdMin " +
                    "and ThresholdMax");
                return false;
            }

            if (hasRange)
            {
                if (!entryNode.ReadFloat(ThresholdMinAttribute, out thresholdMin) ||
                    !entryNode.ReadFloat(ThresholdMaxAttribute, out thresholdMax))
                {
                    return false;
                }

                if (thresholdMax < thresholdMin)
                {
                    creationData.ReportError(
                        $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element's ThresholdMax {thresholdMax} " +
                        $"is below its ThresholdMin {thresholdMin}");
                    return false;
                }
            }
            else
            {
                if (!entryNode.ReadFloat(ThresholdAttribute, out float threshold))
                {
                    return false;
                }

                thresholdMin = threshold;
                thresholdMax = threshold;
            }

            if (_entries.Count > 0 && thresholdMin < _entries[_entries.Count - 1].ThresholdMax)
            {
                creationData.ReportError(
                    $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element is out of order, it starts at " +
                    $"{thresholdMin} and the entry before it reaches {_entries[_entries.Count - 1].ThresholdMax}");
                return false;
            }

            if (!WeightValues.TryRead(entryNode, out WeightValues weights))
            {
                return false;
            }

            AddEntry(new FloatSequenceEntry(thresholdMin, thresholdMax, weights));
            return true;
        }
    }
}
