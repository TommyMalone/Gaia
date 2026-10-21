using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Targets
{
    /// <summary>A way for a configuration to say where, or whom, a component should look at.</summary>
    /// <remarks>
    ///     Targets are built in code as well as loaded, so their values are public and a host may point one at a
    ///     position, an actor or a list in code. Loading fills the same members.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5, Listing 8.5</seealso>
    public abstract class TargetBase : IAiComponent
    {
        /// <summary>Whether this kind of target represents an entity at all, as opposed to only a position.</summary>
        public virtual bool HasEntity => false;

        /// <summary>The entity this target represents right now, or null if it has none or cannot find it.</summary>
        public virtual EntityBase? Entity => null;

        /// <summary>True if the target can be used right now. A target that tracks an entity is invalid while it cannot find one.</summary>
        public virtual bool IsValid => true;

        /// <summary>Loads this component's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived target's own configuration.</summary>
        /// <remarks>
        ///     The factory has already refused whatever the description does not name, and the default returns true, so a
        ///     target with nothing of its own to load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Finds where the target is. A target with an Entity reports that entity's Position.</summary>
        /// <returns>True if the target has a position to give.</returns>
        public abstract bool TryGetPosition(out Vector3 position);

        /// <summary>Finds where the target is, which way it faces and how it is scaled, as one transform.</summary>
        /// <remarks>
        ///     The default pairs TryGetPosition's answer with Rotation.Identity and a scale of (1, 1, 1) and fails where it
        ///     fails, so a target with no facing of its own needs no override. A target that overrides both reports the same
        ///     position from each.
        /// </remarks>
        /// <returns>True if the target has a transform to give.</returns>
        public virtual bool TryGetTransform(out Transform transform)
        {
            bool found = TryGetPosition(out Vector3 position);
            transform = Transform.Identity.WithOrigin(position);
            return found;
        }
    }
}
