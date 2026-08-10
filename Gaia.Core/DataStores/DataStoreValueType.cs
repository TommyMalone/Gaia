namespace Gaia.Core.DataStores
{
    /// <summary>The kinds of value a data store can hold.</summary>
    /// <seealso>"Modular AI", Section 8.4.3</seealso>
    public enum DataStoreValueType
    {
        Bool,
        Int,
        Float,
        String,
        Vector3,
        Rotation,
        Entity,
        EntityList,
    }
}
