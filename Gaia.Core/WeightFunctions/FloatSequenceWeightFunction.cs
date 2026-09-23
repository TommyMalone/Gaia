using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Services.Random;
using Gaia.Core.Specifications;

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
        private static readonly DescriptionBuilder EntryBuilder =
            new("One band of inputs and the weight values it reports.");

        private static readonly AttributeDescription ThresholdAttribute = EntryBuilder.Optional("Threshold",
            AttributeKind.Float, null,
            "The highest input this band covers. Given alone, or ThresholdMin and ThresholdMax are given instead.");

        private static readonly AttributeDescription ThresholdMinAttribute = EntryBuilder.Optional("ThresholdMin",
            AttributeKind.Float, null,
            "The lowest value a rolled threshold can take, given with ThresholdMax in place of Threshold.");

        private static readonly AttributeDescription ThresholdMaxAttribute = EntryBuilder.Optional("ThresholdMax",
            AttributeKind.Float, null,
            "The highest value a rolled threshold can take, given with ThresholdMin in place of Threshold.");

        private static readonly AttributeDescription RerollOnSelectAttribute = EntryBuilder.Optional("RerollOnSelect",
            AttributeKind.Bool, "true", "Whether a rolled threshold is rolled again each time the option is selected.");

        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.WeightFunctions,
            "Divides the inputs into bands by threshold and reports a set of weight values for each band.");

        private static readonly SlotDescription EntrySlot = Builder.Slot("Entry",
            EntryBuilder.Add(WeightValues.Attributes).Build(), 0, SlotDescription.Unbounded);

        private static readonly SlotDescription DefaultSlot = Builder.Slot("Default", WeightValues.Description, 0, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

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
        public override bool ConvertsFloat => true;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            RandomSource = creationData.RandomSource;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == DefaultSlot.Name)
                {
                    if (!WeightValues.TryRead(child, out WeightValues defaultWeights))
                    {
                        return false;
                    }

                    DefaultWeights = defaultWeights;
                }
                else if (child.Name == EntrySlot.Name)
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
        ///     <para>
        ///         An entry that rolls its threshold has to stay in order whatever it rolls, so what is compared is the new
        ///         entry's <see cref="FloatSequenceEntry.ThresholdMin" /> against the previous entry's
        ///         <see cref="FloatSequenceEntry.ThresholdMax" />. Two ranges that overlap would swap places on some rolls and not
        ///         others, and a configuration that does that is never what was meant.
        ///     </para>
        ///     <para>
        ///         A randomized entry takes its first roll here, after the order check, so an entry the check refuses draws
        ///         nothing from <see cref="RandomSource" />. Every entry reaches a function through this method, loaded or built
        ///         in code, so the roll happens once wherever the entry came from.
        ///     </para>
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
                        $"A float sequence entry starting at {entry.ThresholdMin} cannot follow one reaching {previousEntry.ThresholdMax}. Entries must be added in ascending order and must not overlap.",
                        nameof(entry));
                }
            }

            _entries.Add(entry);
            entry.RollThreshold(RandomSource);
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
        ///     Rolls a fresh threshold for every randomized entry whose <see cref="FloatSequenceEntry.RerollOnSelect" />
        ///     holds. Entries with a fixed threshold are the common case and are left alone.
        /// </summary>
        protected override void OnSelect()
        {
            foreach (FloatSequenceEntry entry in _entries)
            {
                if (entry.RerollOnSelect)
                {
                    entry.RollThreshold(RandomSource);
                }
            }
        }

        /// <summary>Loads one entry from an Entry element, in either its fixed or its rolled threshold form.</summary>
        /// <remarks>
        ///     The checks <see cref="AddEntry" /> and the entry's constructor enforce with exceptions are made here first and
        ///     reported through the creation data's <see cref="Services.Log.ILogSink" /> instead, so a configuration mistake reads
        ///     as an error naming the source document and line rather than a thrown exception.
        /// </remarks>
        private bool TryLoadEntry(SpecificationNode entryNode, AiCreationData creationData)
        {
            float thresholdMin;
            float thresholdMax;

            bool hasRange = entryNode.HasAttribute(ThresholdMinAttribute.Name) ||
                            entryNode.HasAttribute(ThresholdMaxAttribute.Name);

            if (hasRange && entryNode.HasAttribute(ThresholdAttribute.Name))
            {
                creationData.ReportError(
                    $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element mixes Threshold with ThresholdMin and ThresholdMax");
                return false;
            }

            if (hasRange)
            {
                if (!entryNode.ReadFloat(ThresholdMinAttribute.Name, out thresholdMin) ||
                    !entryNode.ReadFloat(ThresholdMaxAttribute.Name, out thresholdMax))
                {
                    return false;
                }

                if (thresholdMax < thresholdMin)
                {
                    creationData.ReportError(
                        $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element's ThresholdMax {thresholdMax} is below its ThresholdMin {thresholdMin}");
                    return false;
                }
            }
            else
            {
                if (!entryNode.ReadFloat(ThresholdAttribute.Name, out float threshold))
                {
                    return false;
                }

                thresholdMin = threshold;
                thresholdMax = threshold;
            }

            if (_entries.Count > 0 && thresholdMin < _entries[_entries.Count - 1].ThresholdMax)
            {
                creationData.ReportError(
                    $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element is out of order, it starts at {thresholdMin} and the entry before it reaches {_entries[_entries.Count - 1].ThresholdMax}");
                return false;
            }

            if (!WeightValues.TryRead(entryNode, out WeightValues weights))
            {
                return false;
            }

            bool rerollOnSelect = true;

            if (entryNode.HasAttribute(RerollOnSelectAttribute.Name))
            {
                if (thresholdMax <= thresholdMin)
                {
                    creationData.ReportError(
                        $"Line {entryNode.LineNumber}, the {entryNode.Name.Text} element sets RerollOnSelect on a fixed threshold");
                    return false;
                }

                if (!entryNode.ReadBool(RerollOnSelectAttribute.Name, out rerollOnSelect))
                {
                    return false;
                }
            }

            AddEntry(new FloatSequenceEntry(thresholdMin, thresholdMax, weights)
            {
                RerollOnSelect = rerollOnSelect,
            });
            return true;
        }
    }
}
