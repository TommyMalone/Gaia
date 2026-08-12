using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    public class BooleanWeightFunction : WeightFunctionBase
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
        public override bool Init(AiCreationData creationData)
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
