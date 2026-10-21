using Gaia.Core.Blackboards;

namespace Gaia.Core.Entities
{
    /// <summary>Something a character knows about and should consider reacting to.</summary>
    /// <remarks>Threats live on the <see cref="BrainBlackboard" /> of the brain that holds them, as contacts do.</remarks>
    /// <seealso>"Modular AI", Section 8.4.3.5</seealso>
    public class Threat : EntityBase
    {
        /// <summary>The entity this threat came from, or null if it has none, such as an explosion.</summary>
        /// <remarks>
        ///     The host writes the threat's <see cref="EntityBase.Position" /> separately from the source's, so for a bullet
        ///     impact it holds the point where the round landed rather than the shooter's position. It stays the same when the
        ///     source becomes invalid or Source is set to null.
        /// </remarks>
        public EntityBase? Source { get; set; }

        /// <summary>True if Source is not null and its <see cref="EntityBase.IsValid" /> is true.</summary>
        /// <remarks>
        ///     Setting the source's <see cref="EntityBase.IsValid" /> to false leaves the threat's own true. A character that
        ///     saw an impact still has something to react to after whoever fired it has left its awareness.
        /// </remarks>
        public bool HasValidSource => Source is { IsValid: true };
    }
}
