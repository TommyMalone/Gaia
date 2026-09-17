using System.Collections.Generic;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Reads a named entity list out of a data store and reports whether anything in it still exists.</summary>
    /// <remarks>
    ///     <para>
    ///         The chapter's EntityExists walks the contacts, actors or threats looking for one that meets a set of
    ///         constraints, with a picker doing the narrowing. Pickers are later work, so this reads a list someone else has
    ///         filled and asks only whether any entry is still valid. The answer goes to
    ///         <see cref="WeightFunctionBase.CalculateBool" /> on the weight function, which is what lets one configuration
    ///         veto when nothing is found and another veto when something is.
    ///     </para>
    ///     <para>
    ///         Each entity's <see cref="EntityBase.IsValid" /> is checked rather than the list's Count, so a list that was
    ///         never cleaned up cannot keep an option alive after everything in it has gone.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.1</seealso>
    public sealed class EntityExistsConsideration : VariableConsiderationBase
    {
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
