using Gaia.Core.Blackboards;

namespace Gaia.Core.Entities
{
    /// <summary>Something a character knows about and should consider reacting to.</summary>
    /// <remarks>
    ///     Section 8.4.3.5 names enemy characters alongside explosions and bullet impacts, so a threat always has a
    ///     position but only sometimes has an entity behind it. Threats live on the <see cref="BrainBlackboard" /> like
    ///     contacts.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.5</seealso>
    public class Threat : EntityBase
    {
        /// <summary>The entity this threat came from, or null if it has none, such as an explosion.</summary>
        /// <remarks>
        ///     The position is the threat's own rather than being read through the source, because the point a round landed
        ///     is not where the shooter is, and because it has to survive the source going away.
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
