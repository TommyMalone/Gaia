using System.Collections.Generic;
using Gaia.Core.Strings;

namespace Gaia.Core.DataStores
{
    /// <summary>Walks the names a data store holds, one <see cref="DataStoreEntry" /> at a time.</summary>
    /// <remarks>
    ///     <para>
    ///         The enumerator is a struct rather than an IEnumerator implementation, so a foreach over
    ///         <see cref="DataStoreBase.Entries" /> keeps it on the stack and allocates nothing.
    ///     </para>
    ///     <para>
    ///         The walk reads the index of names and kinds the store is keeping rather than a copy taken when it started.
    ///         Writing a new name while it runs therefore makes the next MoveNext throw, as it would on the dictionary itself.
    ///     </para>
    /// </remarks>
    public struct DataStoreEntryEnumerator
    {
        /// <summary>False for a store that has never been written to, since its index is allocated on the first write.</summary>
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
            // A default Dictionary enumerator holds no dictionary, so calling MoveNext on it would throw. A store whose
            // index has not been allocated has _hasIndex false, and the walk ends without that call.
            return _hasIndex && _index.MoveNext();
        }
    }
}
