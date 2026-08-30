using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a bool out of a data store.</summary>
    public sealed class BooleanVariableConsideration : VariableConsiderationBase
    {
        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore)
        {
            return dataStore.TryGetBool(VariableName, out bool value)
                ? weightFunction.CalculateBool(value)
                : MissingVariableWeights;
        }
    }
}
