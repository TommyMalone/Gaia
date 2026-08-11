using Gaia.Core.DataStores;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a float out of a data store.</summary>
    public sealed class FloatVariableConsideration : VariableConsiderationBase
    {
        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetFloat(VariableName, out float value)
                ? weightFunction.CalcFloat(value)
                : MissingVariableWeights;
        }
    }
}
