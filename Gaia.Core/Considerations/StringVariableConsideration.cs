using Gaia.Core.DataStores;
using Gaia.Core.Strings;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a string out of a data store.</summary>
    public sealed class StringVariableConsideration : VariableConsiderationBase
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
            return dataStore.TryGetString(VariableName, out AiString value)
                ? weightFunction.CalcString(value)
                : MissingVariableWeights;
        }
    }
}
