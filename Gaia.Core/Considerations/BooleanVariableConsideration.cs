using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a bool out of a data store.</summary>
    public sealed class BooleanVariableConsideration : VariableConsiderationBase
    {
        internal static readonly ComponentDescription Description =
            Describe("Reads a true or false value from a blackboard.", DataStoreValueType.Bool);

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
