using Gaia.Core.DataStores;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a bool out of a data store.</summary>
    public sealed class BooleanVariableConsideration : VariableConsiderationBase
    {
        /// <summary>Creates a consideration that a configuration fills.</summary>
        internal BooleanVariableConsideration()
        {
        }

        /// <inheritdoc />
        protected override string QueryPhrase => "a bool";

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsBool;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetBool(VariableName, out bool value)
                ? weightFunction.CalculateBool(value)
                : GetMissingVariableWeights();
        }
    }
}
