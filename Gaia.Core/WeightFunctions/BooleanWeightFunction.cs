using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    public class BooleanWeightFunction : WeightFunctionBase
    {
        /// <summary>Creates a function that reports the given values for true and for false.</summary>
        internal BooleanWeightFunction(WeightValues trueWeight, WeightValues falseWeight)
        {
            TrueWeight = trueWeight;
            FalseWeight = falseWeight;
        }

        /// <summary>The weight values to report for a true input.</summary>
        public WeightValues TrueWeight { get; internal set; }

        /// <summary>The weight values to report for a false input.</summary>
        public WeightValues FalseWeight { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override WeightValues CalcBool(bool value)
        {
            return value ? TrueWeight : FalseWeight;
        }
    }
}