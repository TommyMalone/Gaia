using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that reports fixed values, so that an option can be tuned by hand.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.3</seealso>
    public sealed class TuningConsideration : ConsiderationBase
    {
        /// <summary>Creates a consideration that reports Neutral until a configuration loads.</summary>
        internal TuningConsideration() : this(WeightValues.Neutral)
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

            if (!WeightValues.TryRead(node, out WeightValues weights))
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
