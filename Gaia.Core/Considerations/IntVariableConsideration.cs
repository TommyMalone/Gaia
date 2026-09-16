using Gaia.Core.DataStores;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads an int out of a data store.</summary>
    public sealed class IntVariableConsideration : VariableConsiderationBase
    {
        /// <inheritdoc />
        protected override string QueryPhrase => "an int";

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsInt;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetInt(VariableName, out int value)
                ? weightFunction.CalculateInt(value)
                : GetMissingVariableWeights();
        }
    }
}
