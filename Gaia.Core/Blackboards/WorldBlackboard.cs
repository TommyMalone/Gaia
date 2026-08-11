using Gaia.Core.DataStores;

namespace Gaia.Core.Blackboards
{
    /// <summary>The one set of named values every AI in the game can read and write.</summary>
    /// <remarks>
    ///     Used for anything that should be available regardless of which character a component belongs to or which side
    ///     that character is on, and for passing information between the game and the AI.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.1</seealso>
    public sealed class WorldBlackboard : DataStoreBase
    {
    }
}
