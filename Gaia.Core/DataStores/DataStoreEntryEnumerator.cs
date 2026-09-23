using System.Collections.Generic;
using Gaia.Core.Strings;

namespace Gaia.Core.DataStores
{
    /// <summary>Walks the names a data store holds, one <see cref="DataStoreEntry" /> at a time.</summary>
    /// <remarks>
    ///     <para>
    ///         A struct rather than an IEnumerator implementation, so a foreach over <see cref="DataStoreBase.Entries" />
    ///         keeps the enumerator on the stack and allocates nothing. GetEnumerator hands back a copy of this value, which
    ///         is what the foreach statement walks.
    ///     </para>
    ///     <para>
    ///         The walk reads the index of names and kinds the store is keeping rather than a copy taken when it started, so
    ///         adding a name while it runs ends it the way it ends any dictionary's own enumeration.
    ///     </para>
    /// </remarks>
    public struct DataStoreEntryEnumerator
    {
        /// <summary>False for a store that holds no names, since its index is allocated on the first write.</summary>
        private readonly bool _hasIndex;

        private Dictionary<AiString, DataStoreValueType>.Enumerator _index;

        /// <summary>Creates an enumerator over a store's index of names and kinds, or over nothing when it has none.</summary>
        internal DataStoreEntryEnumerator(Dictionary<AiString, DataStoreValueType>? index)
        {
            _hasIndex = index != null;
            _index = index == null ? default : index.GetEnumerator();
        }

        /// <summary>The entry the walk has reached.</summary>
        public DataStoreEntry Current => new(_index.Current.Key, _index.Current.Value);

        /// <summary>Hands back this enumerator, which is what a foreach statement asks a collection for.</summary>
        public DataStoreEntryEnumerator GetEnumerator()
        {
            return this;
        }

        /// <summary>Moves the walk to the next entry.</summary>
        /// <returns>True while an entry is left to read.</returns>
        public bool MoveNext()
        {
            // A default Dictionary enumerator carries no dictionary to move through, so a store whose index has not
            // been allocated answers from the flag rather than moving one.
            return _hasIndex && _index.MoveNext();
        }
    }
}
