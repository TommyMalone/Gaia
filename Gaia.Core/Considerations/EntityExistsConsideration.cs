using System.Collections.Generic;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a named entity list out of a data store and reports whether anything in it still exists.</summary>
    /// <remarks>
    ///     Each entity's <see cref="EntityBase.IsValid" /> is checked rather than the list's Count, so a list that was
    ///     never cleaned up cannot keep an option alive after everything in it has gone.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.1</seealso>
    public sealed class EntityExistsConsideration : VariableConsiderationBase
    {
        internal static readonly ComponentDescription Description =
            Describe("Reads a list of entities from a blackboard and answers whether any of them still exists.",
                DataStoreValueType.EntityList);

        /// <summary>Creates a consideration that a configuration fills.</summary>
        internal EntityExistsConsideration()
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
            if (!dataStore.TryGetEntityList(VariableName, out List<EntityBase>? entities))
            {
                return GetMissingVariableWeights("an entity list");
            }

            return weightFunction.CalculateBool(AnyValid(entities!));
        }

        /// <summary>True if at least one entity in the list is still valid.</summary>
        private static bool AnyValid(List<EntityBase> entities)
        {
            foreach (EntityBase entity in entities)
            {
                if (entity.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
