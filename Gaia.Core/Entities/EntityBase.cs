using Gaia.Core.DataStores;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.Entities
{
    /// <summary>Something in the world that the AI can reason about.</summary>
    /// <remarks>
    ///     Name, Position and IsValid are written by the game from outside this assembly, every tick in the case of
    ///     Position, so their setters are public where a configured value's would be internal.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3</seealso>
    public abstract class EntityBase : DataStoreBase
    {
        /// <summary>What this entity is called, for configuration and debug output.</summary>
        public AiString Name { get; set; } = AiString.Empty;

        /// <summary>Where this entity is.</summary>
        public Vector3 Position { get; set; }

        /// <summary>False once the entity no longer represents anything the AI should act on.</summary>
        public bool IsValid { get; set; } = true;
    }
}
