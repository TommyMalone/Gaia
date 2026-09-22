using System;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Reports one set of weight values for a true input and another for a false one.</summary>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class BooleanWeightFunction : WeightFunctionBase
    {
        private static readonly AiString FalseElement = "False";
        private static readonly AiString TrueElement = "True";

        private static readonly SlotDescription FalseSlot = new(FalseElement, "False", WeightValues.Description, 0, 1);
        private static readonly SlotDescription TrueSlot = new(TrueElement, "True", WeightValues.Description, 0, 1);

        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.WeightFunctions,
            "Reports one set of weight values when the input is true and another when it is false.",
            Array.Empty<AttributeDescription>(), new[] { TrueSlot, FalseSlot });

        /// <summary>Creates a function that reports Neutral for either answer until a configuration loads.</summary>
        internal BooleanWeightFunction() : this(WeightValues.Neutral, WeightValues.Neutral)
        {
        }

        /// <summary>Creates a function that reports the given values for true and for false.</summary>
        internal BooleanWeightFunction(WeightValues trueWeight, WeightValues falseWeight)
        {
            TrueWeights = trueWeight;
            FalseWeights = falseWeight;
        }

        /// <summary>The weight values to report for a true input.</summary>
        public WeightValues TrueWeights { get; internal set; }

        /// <summary>The weight values to report for a false input.</summary>
        public WeightValues FalseWeights { get; internal set; }

        /// <inheritdoc />
        public override bool ConvertsBool => true;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == TrueElement)
                {
                    if (!WeightValues.TryRead(child, out WeightValues trueWeights))
                    {
                        return false;
                    }

                    TrueWeights = trueWeights;
                }
                else if (child.Name == FalseElement)
                {
                    if (!WeightValues.TryRead(child, out WeightValues falseWeights))
                    {
                        return false;
                    }

                    FalseWeights = falseWeights;
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
        public override WeightValues CalculateBool(bool value)
        {
            return value ? TrueWeights : FalseWeights;
        }
    }
}
