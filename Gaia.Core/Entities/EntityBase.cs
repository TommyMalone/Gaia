using Gaia.Core.DataStores;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.Entities
{
    /// <summary>Something in the world that the AI can reason about.</summary>
    public abstract class EntityBase : DataStoreBase
    {
        /// <summary>What this entity is called, for configuration and debug output.</summary>
        public AiString Name { get; internal set; } = AiString.Empty;

        /// <summary>Where this entity is.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>False once the entity no longer represents anything the AI should act on.</summary>
        public bool IsValid { get; internal set; } = true;
    }
}
