using Gaia.Core.DataStores;

namespace Gaia.Core.Blackboards
{
    /// <summary>The set of named values belonging to one character, for its own components to share among themselves.</summary>
    /// <remarks>
    ///     Every AI controlled character has one. Knowledge private to the character lives here, such as contacts, the
    ///     entities it knows about, and threats, the contacts it judges dangerous, since what one character believes about the
    ///     world is not something another should read.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.2</seealso>
    public sealed class BrainBlackboard : DataStoreBase
    {
    }
}
