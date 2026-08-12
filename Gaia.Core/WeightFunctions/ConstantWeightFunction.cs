using Gaia.Core.Strings;
using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    public class ConstantWeightFunction : WeightFunctionBase
    {
        /// <summary>The weight values to report for every input.</summary>
        public WeightValues Weights { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
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
