using Gaia.Core.DataStores;

namespace Gaia.Core.Blackboards
{
    /// <summary>
    ///     The one set of named values every AI in the game can read. The host writes it at its sync points, under the
    ///     threading contract DataStoreBase states. A name of it read as another kind is reported under its own LogChannel,
    ///     which the host sets, rather than under the reading brain's.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.4.3.1</seealso>
    public sealed class WorldBlackboard : DataStoreBase
    {
    }
}
