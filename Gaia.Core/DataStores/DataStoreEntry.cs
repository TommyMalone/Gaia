using Gaia.Core.Strings;

namespace Gaia.Core.DataStores
{
    /// <summary>One name a data store holds, with the kind of value it was written as.</summary>
    /// <remarks>
    ///     The value itself is not carried. A reader that wants it calls the typed getter for the kind reported here,
    ///     <see cref="DataStoreBase.TryGetFloat" /> for a <see cref="DataStoreValueType.Float" />, so a read is still a copy
    ///     out of the table that knows what it holds.
    /// </remarks>
    public readonly struct DataStoreEntry
    {
        /// <summary>Creates an entry for one name of a store's index of names and kinds.</summary>
        internal DataStoreEntry(AiString name, DataStoreValueType valueType)
        {
            Name = name;
            ValueType = valueType;
        }

        /// <summary>The name the value was written under.</summary>
        public AiString Name { get; }

        /// <summary>The kind of value the name holds, which says which typed getter reads it.</summary>
        public DataStoreValueType ValueType { get; }
    }
}
