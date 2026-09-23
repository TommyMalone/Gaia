using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     Matches the input against a set of configured strings and reports the weight values belonging to the one it
    ///     matched, or DefaultWeights if it matched none.
    /// </summary>
    /// <remarks>
    ///     Used by considerations that read a named string out of a data store, such as one that vetoes unless the side
    ///     it reads is "Enemy". Matching ignores case, because an <see cref="AiString" /> is hashed from its lower cased text.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class StringWeightFunction : WeightFunctionBase
    {
        private static readonly DescriptionBuilder EntryBuilder =
            new("One piece of text and the weight values it reports.");

        private static readonly AttributeDescription TextAttribute = EntryBuilder.Required("Text", AttributeKind.String,
            "The text this entry answers for, matched without regard to case.");

        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.WeightFunctions,
            "Matches the input against pieces of text and reports the weight values of the one it matches.");

        private static readonly SlotDescription EntrySlot = Builder.Slot("Entry",
            EntryBuilder.Add(WeightValues.Attributes).Build(), 0, SlotDescription.Unbounded);

        private static readonly SlotDescription DefaultSlot = Builder.Slot("Default", WeightValues.Description, 0, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

        private readonly Dictionary<AiString, WeightValues> _entries = new();

        /// <summary>Creates a function with no entries that reports Neutral for every input until a configuration loads.</summary>
        internal StringWeightFunction() : this(WeightValues.Neutral)
        {
        }

        /// <summary>Creates a function with no entries that reports the given values for every input.</summary>
        internal StringWeightFunction(WeightValues defaultWeights)
        {
            DefaultWeights = defaultWeights;
        }

        /// <summary>The weight values to report for an input matching no entry.</summary>
        public WeightValues DefaultWeights { get; internal set; }

        /// <inheritdoc />
        public override bool ConvertsString => true;

        /// <summary>Adds an entry, replacing any entry already configured for the same string.</summary>
        internal void AddEntry(AiString value, WeightValues weights)
        {
            _entries[value] = weights;
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

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
                    if (!child.ReadString(TextAttribute.Name, out string text) ||
                        !WeightValues.TryRead(child, out WeightValues weights))
                    {
                        return false;
                    }

                    AddEntry(text, weights);
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

        /// <inheritdoc />
        public override WeightValues CalculateString(AiString value)
        {
            return _entries.TryGetValue(value, out WeightValues weights) ? weights : DefaultWeights;
        }
    }
}
