using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads an int out of a data store.</summary>
    public sealed class IntVariableConsideration : VariableConsiderationBase
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
            return dataStore.TryGetInt(VariableName, out int value)
                ? weightFunction.CalculateInt(value)
                : MissingVariableWeights;
        }
    }
}
