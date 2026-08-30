using Gaia.Core.Factories;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Reports one set of weight values for a true input and another for a false one.</summary>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class BooleanWeightFunction : WeightFunctionBase
    {
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
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override WeightValues CalculateBool(bool value)
        {
            return value ? TrueWeights : FalseWeights;
        }
    }
}
