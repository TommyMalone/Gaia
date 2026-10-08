using Gaia.Core.DataStores;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.Entities
{
    /// <summary>Something in the world that the AI can reason about.</summary>
    /// <remarks>
    ///     Name, Position, Rotation, Scale and IsValid are written by the game from outside this assembly, every tick in
    ///     the case of Position, Rotation and Scale, so their setters are public where a configured value's would be internal.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3</seealso>
    public abstract class EntityBase : DataStoreBase
    {
        /// <summary>What this entity is called, for configuration and debug output.</summary>
        public AiString Name { get; set; } = AiString.Empty;

        /// <summary>Where this entity is.</summary>
        public Vector3 Position { get; set; }

        /// <summary>
        ///     Which way this entity faces in the world, written by the game. The identity faces the world's forward, along
        ///     Z, and an entity whose facing the game never writes reports the identity, so a reader treats the two alike.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The game writes the facing of the entity's root, a character's body rather than its head or its aim, as
        ///         resolved through whatever hierarchy the game places it under.
        ///     </para>
        ///     <para>
        ///         An actor's facing is the game's own. A contact's is the one its believer last saw, which may differ from the
        ///         actor's as its other values may. A threat's is its own when the game writes one, and the identity when it has
        ///         none, such as an explosion.
        ///     </para>
        /// </remarks>
        public Rotation Rotation { get; set; } = Rotation.Identity;

        /// <summary>
        ///     How this entity is stretched in the world along its own right, up and forward, written by the game, and (1, 1,
        ///     1) until written, which is its authored size.
        /// </summary>
        /// <remarks>
        ///     It is the scale in the world, resolved through whatever hierarchy the game places the entity under, as
        ///     Rotation is. Three numbers hold a world scale exactly unless an ancestor is scaled unevenly in the plane the entity
        ///     is turned in, which skews the shape. Then no three numbers hold it, and the game hands over its engine's nearest
        ///     fit. It follows Rotation's rules for each kind of entity, the game's value on an actor, the last seen on a contact,
        ///     and (1, 1, 1) on a threat with none.
        /// </remarks>
        public Vector3 Scale { get; set; } = new(1f, 1f, 1f);

        /// <summary>False once the entity no longer represents anything the AI should act on.</summary>
        public bool IsValid { get; set; } = true;
    }
}
