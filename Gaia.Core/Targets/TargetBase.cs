using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Targets
{
    /// <summary>A way for a configuration to say where, or whom, a component should look at.</summary>
    /// <remarks>
    ///     <para>
    ///         Every target can be asked for a position, and some can also be asked for an entity. Listing 8.5 keeps two
    ///         questions apart that are easy to collapse. Whether this kind of target ever has an entity is HasEntity, and
    ///         whether it can find one right now is IsValid. A target that looks a character up by name always has an entity
    ///         in the first sense and only sometimes in the second.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5, Listing 8.5</seealso>
    public abstract class TargetBase : IAiComponent
    {
        /// <summary>Whether this kind of target represents an entity at all, as opposed to only a position.</summary>
        /// <remarks>
        ///     A property of the type rather than of the moment. A configuration that needs an entity is checked against
        ///     this, and whether the entity can be found just now is IsValid.
        /// </remarks>
        public virtual bool HasEntity => false;

        /// <summary>The entity this target represents right now, or null if it has none or cannot find it.</summary>
        public virtual EntityBase? Entity => null;

        /// <summary>True if the target can be used this cycle. A target that tracks an entity is invalid while it cannot find one.</summary>
        public virtual bool IsValid => true;

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived target's own configuration.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        protected abstract bool OnInit(AiCreationData creationData);

        /// <summary>Finds where the target is. A target with an Entity reports that entity's Position.</summary>
        /// <returns>True if the target has a position to give.</returns>
        public abstract bool TryGetPosition(out Vector3 position);
    }
}
