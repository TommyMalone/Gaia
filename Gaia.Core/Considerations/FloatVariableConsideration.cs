using Gaia.Core.DataStores;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a float out of a data store.</summary>
    public sealed class FloatVariableConsideration : VariableConsiderationBase
    {
        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetFloat(VariableName, out float value)
                ? weightFunction.CalculateFloat(value)
                : MissingVariableWeights;
        }
    }
}
