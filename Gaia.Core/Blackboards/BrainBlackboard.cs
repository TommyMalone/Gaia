using Gaia.Core.DataStores;

namespace Gaia.Core.Blackboards
{
    /// <summary>The set of named values belonging to one character, for its own components to share among themselves.</summary>
    /// <remarks>
    ///     Every AI controlled character has one. A belief is what one character holds about the world, such as a
    ///     <see cref="Entities.Contact" /> it knows about or a <see cref="Entities.Threat" /> it should consider reacting to.
    ///     Beliefs live here, since what one character believes about the world is not something another should read.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.2</seealso>
    public sealed class BrainBlackboard : DataStoreBase
    {
    }
}
