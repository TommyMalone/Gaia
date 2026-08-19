using System.Collections.Generic;
using Gaia.Core.Blackboards;
using Gaia.Core.Considerations;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;
using Gaia.Core.Temp;

namespace Gaia.Core.Targets
{
    /// <summary>An entity looked up by name in a named entity list.</summary>
    /// <remarks>
    ///     <para>
    ///         The chapter's ByName target searches the actors or the contacts depending on its configuration. Its contacts
    ///         already live on the brain's blackboard, while its actors are held by a manager singleton, and there are no
    ///         singletons here. Both cases therefore read the same way, as a data store and the name of an entity list on it,
    ///         which is the same pair <see cref="EntityExistsConsideration" /> reads. A contact list on the
    ///         <see cref="BrainBlackboard" /> and an actor list on the <see cref="WorldBlackboard" /> are the two cases the
    ///         chapter describes.
    ///     </para>
    ///     <para>
    ///         Find runs on every ask rather than once. An entity can leave the list, or be replaced by a new one under the
    ///         same name, and a cached reference would keep pointing at the old one. An entity that is in the list but no
    ///         longer valid is skipped, so IsValid is false rather than Entity handing back a corpse.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5</seealso>
    public sealed class ByNameTarget : TargetBase
    {
        /// <summary>The store holding the list to search.</summary>
        public DataStoreBase? DataStore { get; internal set; }

        /// <summary>The name of the entity list on the store.</summary>
        public AiString ListName { get; internal set; } = AiString.Empty;

        /// <summary>The name of the entity to find in the list.</summary>
        public AiString EntityName { get; internal set; } = AiString.Empty;

        /// <summary>Always true, since this kind of target is a way of naming an entity.</summary>
        public override bool HasEntity => true;

        /// <summary>The valid entity with EntityName in the list, or null if there is none.</summary>
        public override EntityBase? Entity => Find();

        /// <summary>True while a valid entity with EntityName is in the list.</summary>
        public override bool IsValid => Find() != null;

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            EntityBase? entity = Find();

            if (entity == null)
            {
                position = Vector3.Zero;

                return false;
            }

            position = entity.Position;

            return true;
        }

        /// <summary>
        ///     Searches the list under ListName for a valid entity named EntityName. Null when the store, the list or the
        ///     entity is missing.
        /// </summary>
        private EntityBase? Find()
        {
            if (DataStore == null || !DataStore.TryGetEntityList(ListName, out List<EntityBase>? entities))
            {
                return null;
            }

            foreach (EntityBase entity in entities!)
            {
                if (entity.IsValid && entity.Name == EntityName)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
