using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    public class ConstantWeightFunction : WeightFunctionBase
    {
        /// <summary>The weight values to report for every input.</summary>
        public WeightValues WeightValues { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override WeightValues CalcBool(bool value)
        {
            return WeightValues;
        }

        /// <inheritdoc />
        public override WeightValues CalcFloat(float value)
        {
            return WeightValues;
        }

        /// <inheritdoc />
        public override WeightValues CalcString(string value)
        {
            return WeightValues;
        }
    }
}
