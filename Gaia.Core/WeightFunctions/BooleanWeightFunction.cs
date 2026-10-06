using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Reports one set of weight values for a true input and another for a false one.</summary>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class BooleanWeightFunction : WeightFunctionBase
    {
        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.WeightFunctions,
            "Reports one set of weight values when the input is true and another when it is false.");

        private static readonly SlotDescription TrueSlot = Builder.Slot("True", WeightValues.Description, 0, 1);
        private static readonly SlotDescription FalseSlot = Builder.Slot("False", WeightValues.Description, 0, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

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

            // Every child is a True or a False, since the factory refused anything else against the description.
            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == TrueSlot.Name)
                {
                    if (!WeightValues.TryRead(child, out WeightValues trueWeights))
                    {
                        return false;
                    }

                    TrueWeights = trueWeights;
                }
                else if (child.Name == FalseSlot.Name)
                {
                    if (!WeightValues.TryRead(child, out WeightValues falseWeights))
                    {
                        return false;
                    }

                    FalseWeights = falseWeights;
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
