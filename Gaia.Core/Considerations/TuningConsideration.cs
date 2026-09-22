using System;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that reports fixed values, so that an option can be tuned by hand.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.3</seealso>
    public sealed class TuningConsideration : ConsiderationBase
    {
        /// <summary>What a Tuning element reads as before its own attributes are applied.</summary>
        /// <remarks>
        ///     The general rule is that an unspecified weight value has no effect, and the exception is this consideration,
        ///     whose addend is 1, so that an element writing only a multiplier scales something rather than scaling nothing.
        /// </remarks>
        internal static readonly WeightValues Default = new(1f, 1f, float.MinValue);

        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.Considerations,
            "Fixed values that tune the option by hand.", WeightValues.DescribeAttributes(Default),
            Array.Empty<SlotDescription>());

        /// <summary>Creates a consideration that reports the chapter's Tuning defaults until a configuration loads.</summary>
        internal TuningConsideration() : this(Default)
        {
        }

        /// <summary>Creates a consideration that reports the given values.</summary>
        internal TuningConsideration(WeightValues weights)
        {
            Weights = weights;
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.Children.Count > 0)
            {
                SpecificationNode child = node.Children[0];

                creationData.ReportError(
                    $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                return false;
            }

            if (!WeightValues.TryRead(node, Default, out WeightValues weights))
            {
                return false;
            }

            Weights = weights;
            return true;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
        }
    }
}
