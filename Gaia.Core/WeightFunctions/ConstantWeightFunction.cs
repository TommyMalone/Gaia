using System;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Reports the same weight values whatever the input.</summary>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class ConstantWeightFunction : WeightFunctionBase
    {
        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.WeightFunctions,
            "Reports the same weight values whatever the input.", WeightValues.Attributes,
            Array.Empty<SlotDescription>());

        /// <summary>Creates a function that a configuration fills.</summary>
        internal ConstantWeightFunction()
        {
        }

        /// <summary>The weight values to report for every input.</summary>
        /// <remarks>
        ///     Neutral until something sets it, so a function built in code and left alone contributes nothing, the way an
        ///     element saying nothing does. The default of the struct would be a multiplier of 0 and a rank of 0, which is a veto
        ///     that also claims a rank above the identity.
        /// </remarks>
        public WeightValues Weights { get; internal set; } = WeightValues.Neutral;

        /// <inheritdoc />
        public override bool ConvertsBool => true;

        /// <inheritdoc />
        public override bool ConvertsFloat => true;

        /// <inheritdoc />
        public override bool ConvertsString => true;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!WeightValues.TryRead(node, out WeightValues weights))
            {
                return false;
            }

            Weights = weights;
            return true;
        }

        /// <inheritdoc />
        public override WeightValues CalculateBool(bool value)
        {
            return Weights;
        }

        /// <inheritdoc />
        public override WeightValues CalculateFloat(float value)
        {
            return Weights;
        }

        /// <inheritdoc />
        public override WeightValues CalculateString(AiString value)
        {
            return Weights;
        }
    }
}
