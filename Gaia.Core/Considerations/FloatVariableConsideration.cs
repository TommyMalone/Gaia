using Gaia.Core.DataStores;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a float out of a data store.</summary>
    public sealed class FloatVariableConsideration : VariableConsiderationBase
    {
        /// <summary>Creates a consideration that a configuration fills.</summary>
        internal FloatVariableConsideration()
        {
        }

        /// <inheritdoc />
        protected override string QueryPhrase => "a float";

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsFloat;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetFloat(VariableName, out float value)
                ? weightFunction.CalculateFloat(value)
                : GetMissingVariableWeights();
        }
    }
}
