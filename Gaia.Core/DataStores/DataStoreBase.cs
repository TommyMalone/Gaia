using System.Collections.Generic;
using System.Threading;
using Gaia.Core.Entities;
using Gaia.Core.Services.Log;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.DataStores
{
    /// <summary>A set of named values that components can write to and read from.</summary>
    /// <remarks>
    ///     <para>
    ///         Data stores are the basis for the blackboards and for the entities, so a component can share information
    ///         without the rest of the AI knowing what is stored or what type it is.
    ///     </para>
    ///     <para>
    ///         Each kind of value has a table of its own rather than one table of type-erased values. A single table would
    ///         have to hold everything as object, which means allocating a "box" on the heap for every float or int written
    ///         and "unboxing" it again on every read. A table that already knows what it holds keeps those values inside its
    ///         own storage, so a write allocates nothing, a read is a copy rather than a cast, and neither leaves anything
    ///         behind for the collector. Each table is also free to be sized or replaced on its own.
    ///     </para>
    ///     <para>Names are <see cref="AiString" /> values, so a lookup compares hashes and matching ignores case.</para>
    ///     <para>
    ///         The threading contract is reads from any thread, writes synchronized by the host. A brain's own blackboard is
    ///         written and read on that brain's thread, and a shared store such as the world blackboard takes its writes at
    ///         the host's sync points while brains only read it mid-cycle. The mismatch throttle below is the one mutation a
    ///         read can cause, and it locks itself so concurrent readers stay safe.
    ///     </para>
    ///     <para>
    ///         Each table allocates on its first write rather than at construction, so a store holds tables only for the
    ///         kinds it stores. A freshly spawned entity, such as a <see cref="Entities.Threat" /> for one bullet impact,
    ///         costs no tables at all until something is written to it, which is what keeps high-churn entities cheap to
    ///         create.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3</seealso>
    public abstract class DataStoreBase
    {
        /// <summary>
        ///     The sink a store reports through until a host or a loader wires one. Shared, where the sinks on the components
        ///     are per instance, because a store is the one a game allocates per entity and this sink holds no state worth an
        ///     allocation each time.
        /// </summary>
        private static readonly TraceLogSink DefaultLogSink = new();

        private Dictionary<AiString, bool>? _bools;
        private Dictionary<AiString, EntityBase>? _entities;
        private Dictionary<AiString, List<EntityBase>>? _entityLists;
        private Dictionary<AiString, float>? _floats;
        private Dictionary<AiString, int>? _ints;

        /// <summary>The names whose kind mismatch has already been reported, so each warns once.</summary>
        private HashSet<AiString>? _mismatchWarnedNames;

        private Dictionary<AiString, Rotation>? _rotations;
        private Dictionary<AiString, AiString>? _strings;

        /// <summary>Which kind of value each name holds, pointing at the table it can be read from.</summary>
        private Dictionary<AiString, DataStoreValueType>? _valueTypes;

        private Dictionary<AiString, Vector3>? _vector3s;

        /// <summary>Where kind mismatches are reported.</summary>
        /// <remarks>
        ///     Public rather than internal set because a host makes stores of its own, the world blackboard and every
        ///     entity's, as often as loading makes them. A loaded brain stamps its own blackboard's sink and channel, and a host
        ///     wires the stores it makes itself.
        /// </remarks>
        public ILogSink LogSink { get; set; } = DefaultLogSink;

        /// <summary>What mismatch reports are filed under, usually the owning character.</summary>
        public AiString LogChannel { get; set; } = AiString.Empty;

        /// <summary>Reports whether a value has been written under the given name.</summary>
        public bool Contains(AiString name)
        {
            return _valueTypes != null && _valueTypes.ContainsKey(name);
        }

        /// <summary>Reports which kind of value a name holds.</summary>
        /// <returns>True if the name holds a value.</returns>
        public bool TryGetValueType(AiString name, out DataStoreValueType valueType)
        {
            valueType = default;

            return _valueTypes != null && _valueTypes.TryGetValue(name, out valueType);
        }

        /// <summary>Writes a bool under the given name.</summary>
        public void SetBool(AiString name, bool value)
        {
            PrepareToSet(name, DataStoreValueType.Bool);

            _bools ??= new Dictionary<AiString, bool>();
            _bools[name] = value;
        }

        /// <summary>Reads the bool written under the given name.</summary>
        /// <returns>True if the name holds a bool.</returns>
        public bool TryGetBool(AiString name, out bool value)
        {
            if (_bools != null && _bools.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.Bool);

            return false;
        }

        /// <summary>Writes an int under the given name.</summary>
        public void SetInt(AiString name, int value)
        {
            PrepareToSet(name, DataStoreValueType.Int);

            _ints ??= new Dictionary<AiString, int>();
            _ints[name] = value;
        }

        /// <summary>Reads the int written under the given name.</summary>
        /// <returns>True if the name holds an int.</returns>
        public bool TryGetInt(AiString name, out int value)
        {
            if (_ints != null && _ints.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.Int);

            return false;
        }

        /// <summary>Writes a float under the given name.</summary>
        public void SetFloat(AiString name, float value)
        {
            PrepareToSet(name, DataStoreValueType.Float);

            _floats ??= new Dictionary<AiString, float>();
            _floats[name] = value;
        }

        /// <summary>Reads the float written under the given name.</summary>
        /// <returns>True if the name holds a float.</returns>
        public bool TryGetFloat(AiString name, out float value)
        {
            if (_floats != null && _floats.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.Float);

            return false;
        }

        /// <summary>Writes a string under the given name.</summary>
        public void SetString(AiString name, AiString value)
        {
            PrepareToSet(name, DataStoreValueType.String);

            _strings ??= new Dictionary<AiString, AiString>();
            _strings[name] = value;
        }

        /// <summary>Reads the string written under the given name.</summary>
        /// <returns>True if the name holds a string.</returns>
        public bool TryGetString(AiString name, out AiString value)
        {
            if (_strings != null && _strings.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.String);

            return false;
        }

        /// <summary>Writes a vector under the given name.</summary>
        public void SetVector3(AiString name, Vector3 value)
        {
            PrepareToSet(name, DataStoreValueType.Vector3);

            _vector3s ??= new Dictionary<AiString, Vector3>();
            _vector3s[name] = value;
        }

        /// <summary>Reads the vector written under the given name.</summary>
        /// <returns>True if the name holds a vector.</returns>
        public bool TryGetVector3(AiString name, out Vector3 value)
        {
            if (_vector3s != null && _vector3s.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.Vector3);

            return false;
        }

        /// <summary>Writes a rotation under the given name.</summary>
        public void SetRotation(AiString name, Rotation value)
        {
            PrepareToSet(name, DataStoreValueType.Rotation);

            _rotations ??= new Dictionary<AiString, Rotation>();
            _rotations[name] = value;
        }

        /// <summary>Reads the rotation written under the given name.</summary>
        /// <returns>True if the name holds a rotation.</returns>
        public bool TryGetRotation(AiString name, out Rotation value)
        {
            if (_rotations != null && _rotations.TryGetValue(name, out value))
            {
                return true;
            }

            value = default;
            ReportAnyTypeMismatch(name, DataStoreValueType.Rotation);

            return false;
        }

        /// <summary>Writes a single entity under the given name.</summary>
        public void SetEntity(AiString name, EntityBase value)
        {
            PrepareToSet(name, DataStoreValueType.Entity);

            _entities ??= new Dictionary<AiString, EntityBase>();
            _entities[name] = value;
        }

        /// <summary>Reads the single entity written under the given name.</summary>
        /// <returns>True if the name holds a single entity.</returns>
        public bool TryGetEntity(AiString name, out EntityBase? value)
        {
            if (_entities != null && _entities.TryGetValue(name, out value))
            {
                return true;
            }

            value = null;
            ReportAnyTypeMismatch(name, DataStoreValueType.Entity);

            return false;
        }

        /// <summary>
        ///     Writes a list of entities under the given name. Stores the list itself rather than a copy, so the host that
        ///     wrote it can add and remove entities in place.
        /// </summary>
        public void SetEntityList(AiString name, List<EntityBase> value)
        {
            PrepareToSet(name, DataStoreValueType.EntityList);

            _entityLists ??= new Dictionary<AiString, List<EntityBase>>();
            _entityLists[name] = value;
        }

        /// <summary>Reads the list of entities written under the given name.</summary>
        /// <returns>True if the name holds a list of entities.</returns>
        public bool TryGetEntityList(AiString name, out List<EntityBase>? value)
        {
            if (_entityLists != null && _entityLists.TryGetValue(name, out value))
            {
                return true;
            }

            value = null;
            ReportAnyTypeMismatch(name, DataStoreValueType.EntityList);

            return false;
        }

        /// <summary>Forgets whatever was written under the given name.</summary>
        /// <returns>True if the name held a value.</returns>
        public bool Remove(AiString name)
        {
            if (_valueTypes == null || !_valueTypes.TryGetValue(name, out DataStoreValueType valueType))
            {
                return false;
            }

            RemoveValue(name, valueType);
            _valueTypes.Remove(name);

            return true;
        }

        /// <summary>Forgets every name in the store. The tables keep their capacity, so a pooled store refills without allocating.</summary>
        public void Clear()
        {
            _valueTypes?.Clear();
            _mismatchWarnedNames?.Clear();

            _bools?.Clear();
            _ints?.Clear();
            _floats?.Clear();
            _strings?.Clear();
            _vector3s?.Clear();
            _rotations?.Clear();
            _entities?.Clear();
            _entityLists?.Clear();
        }

        /// <summary>Called when a name is read or written as a kind other than the one it already holds.</summary>
        /// <remarks>
        ///     A data store is in effect global memory, so two unrelated sets of components can end up using the same name. A
        ///     mismatched kind is the most detectable form of that collision.
        /// </remarks>
        protected virtual void OnTypeMismatch(
            AiString name,
            DataStoreValueType storedValueType,
            DataStoreValueType requestedValueType)
        {
            // Reads may run concurrently under the store's threading contract, and a failed read lands here, so
            // the throttle set is installed with a compare-exchange and mutated under its own lock.
            if (_mismatchWarnedNames == null)
            {
                Interlocked.CompareExchange(ref _mismatchWarnedNames, new HashSet<AiString>(), null);
            }

            lock (_mismatchWarnedNames!)
            {
                if (!_mismatchWarnedNames.Add(name))
                {
                    return;
                }
            }

            if (LogSink.IsEnabled(LogSeverity.Warning))
            {
                LogSink.LogWarning(LogChannel,
                    $"the {name.Text} name was written as {storedValueType} and used as {requestedValueType}");
            }
        }

        /// <summary>
        ///     Records the kind a write is about to store under a name, evicting whatever value of another kind the name was
        ///     holding.
        /// </summary>
        private void PrepareToSet(AiString name, DataStoreValueType valueType)
        {
            _valueTypes ??= new Dictionary<AiString, DataStoreValueType>();

            if (_valueTypes.TryGetValue(name, out DataStoreValueType storedValueType) && storedValueType != valueType)
            {
                // The old value has to go, or the name would sit in two tables at once and _valueTypes could only
                // point at one of them.
                OnTypeMismatch(name, storedValueType, valueType);
                RemoveValue(name, storedValueType);
            }

            _valueTypes[name] = valueType;
        }

        /// <summary>Reports a mismatch if the failed read was one, rather than a name that was never written.</summary>
        /// <remarks>
        ///     The extra lookup this needs runs only on reads that already failed, so a read that finds its value pays
        ///     nothing and a consideration that expects a miss pays one lookup a cycle. A name that was never written is an
        ///     ordinary miss rather than a collision.
        /// </remarks>
        private void ReportAnyTypeMismatch(AiString name, DataStoreValueType requestedValueType)
        {
            if (_valueTypes == null || !_valueTypes.TryGetValue(name, out DataStoreValueType storedValueType))
            {
                return;
            }

            OnTypeMismatch(name, storedValueType, requestedValueType);
        }

        /// <summary>Removes the value a name holds from the table matching its kind.</summary>
        private void RemoveValue(AiString name, DataStoreValueType valueType)
        {
            switch (valueType)
            {
                case DataStoreValueType.Bool:
                    _bools?.Remove(name);
                    break;
                case DataStoreValueType.Int:
                    _ints?.Remove(name);
                    break;
                case DataStoreValueType.Float:
                    _floats?.Remove(name);
                    break;
                case DataStoreValueType.String:
                    _strings?.Remove(name);
                    break;
                case DataStoreValueType.Vector3:
                    _vector3s?.Remove(name);
                    break;
                case DataStoreValueType.Rotation:
                    _rotations?.Remove(name);
                    break;
                case DataStoreValueType.Entity:
                    _entities?.Remove(name);
                    break;
                case DataStoreValueType.EntityList:
                    _entityLists?.Remove(name);
                    break;
            }
        }
    }
}
