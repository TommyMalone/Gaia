using Gaia.Core.DataStores;
using Gaia.Core.Strings;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a string out of a data store.</summary>
    public sealed class StringVariableConsideration : VariableConsiderationBase
    {
        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetString(VariableName, out AiString value)
                ? weightFunction.CalculateString(value)
                : GetMissingVariableWeights("as a string");
        }
    }
}
