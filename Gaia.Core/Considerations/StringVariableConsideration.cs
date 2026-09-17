using Gaia.Core.DataStores;
using Gaia.Core.Strings;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a string out of a data store.</summary>
    public sealed class StringVariableConsideration : VariableConsiderationBase
    {
        /// <summary>Creates a consideration that a configuration fills.</summary>
        internal StringVariableConsideration()
        {
        }

        /// <inheritdoc />
        protected override string QueryPhrase => "a string";

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsString;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetString(VariableName, out AiString value)
                ? weightFunction.CalculateString(value)
                : GetMissingVariableWeights();
        }
    }
}
