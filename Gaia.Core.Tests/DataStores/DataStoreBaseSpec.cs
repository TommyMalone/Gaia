using Gaia.Core.Blackboards;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.Services.Log;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.DataStores;

/// <summary>Covers how a <see cref="DataStoreBase" /> holds named values, the part every blackboard and entity shares.</summary>
[TestFixture]
public class DataStoreBaseSpec
{
    [SetUp]
    public void BuildDataStore()
    {
        _dataStore = new StubDataStore();
        _guard = new StubEntity("Guard");

        // The tests about mismatch reports read them off a store wired to the fixture's sink.
        _logSink = new RecordingLogSink();
        _warningStore = new BrainBlackboard
        {
            LogSink = _logSink,
            LogChannel = "Guard",
        };
    }

    /// <summary>
    ///     Every kind a store holds, read from the enumeration. Write and TryRead below name each kind, so a kind added
    ///     later fails the tests that walk the kinds until both learn it.
    /// </summary>
    private static readonly DataStoreValueType[] ValueTypes = Enum.GetValues<DataStoreValueType>();

    private StubDataStore _dataStore;
    private StubEntity _guard;

    private RecordingLogSink _logSink;
    private BrainBlackboard _warningStore;

    [Test]
    public void ReportsABoolItWasGiven()
    {
        _dataStore.SetBool("IsOfficer", true);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetBool("IsOfficer", out bool isOfficer), Is.True);
            Assert.That(isOfficer, Is.True);
        });
    }

    [Test]
    public void ReportsAnIntItWasGiven()
    {
        _dataStore.SetInt("AmmoCount", 7);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetInt("AmmoCount", out int ammoCount), Is.True);
            Assert.That(ammoCount, Is.EqualTo(7));
        });
    }

    [Test]
    public void ReportsAFloatItWasGiven()
    {
        _dataStore.SetFloat("Health", 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetFloat("Health", out float health), Is.True);
            Assert.That(health, Is.EqualTo(0.5f));
        });
    }

    [Test]
    public void ReportsAStringItWasGiven()
    {
        _dataStore.SetString("Side", "Enemy");

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetString("Side", out AiString side), Is.True);
            Assert.That(side.Text, Is.EqualTo("Enemy"));
        });
    }

    [Test]
    public void ReportsAVectorItWasGiven()
    {
        Vector3 lastKnownPosition = new(1f, 2f, 3f);

        _dataStore.SetVector3("LastKnownPosition", lastKnownPosition);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetVector3("LastKnownPosition", out Vector3 storedPosition), Is.True);
            Assert.That(storedPosition, Is.EqualTo(lastKnownPosition));
        });
    }

    [Test]
    public void ReportsARotationItWasGiven()
    {
        Rotation facing = Rotation.FromEulerAngles(0f, 90f, 0f);

        _dataStore.SetRotation("Facing", facing);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetRotation("Facing", out Rotation storedFacing), Is.True);
            Assert.That(storedFacing, Is.EqualTo(facing));
        });
    }

    [Test]
    public void ReportsAnEntityItWasGiven()
    {
        _dataStore.SetEntity("SnipeTarget", _guard);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetEntity("SnipeTarget", out EntityBase? storedTarget), Is.True);

            // The store holds the entity itself rather than a copy of what it contained.
            Assert.That(storedTarget, Is.SameAs(_guard));
        });
    }

    [Test]
    public void ReportsAnEntityListItWasGiven()
    {
        StubEntity firstTarget = new("Guard");
        StubEntity secondTarget = new("Officer");

        List<EntityBase> targets = new() { firstTarget, secondTarget };

        _dataStore.SetEntityList("SnipeTargets", targets);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetEntityList("SnipeTargets", out List<EntityBase>? storedTargets), Is.True);

            // The host fills the list in place, so the store has to hold the list rather than a copy of it.
            Assert.That(storedTargets, Is.SameAs(targets));
        });
    }

    [Test]
    public void ReportsAMissForANameItWasNeverGiven()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.Contains("Side"), Is.False);
            Assert.That(_dataStore.TryGetString("Side", out AiString _), Is.False);

            Assert.That(_dataStore.TypeMismatches, Is.Empty);
        });
    }

    [Test]
    public void LeavesTheEmptyStringForANameItHoldsNoStringUnder()
    {
        bool isFound = _dataStore.TryGetString("Target", out AiString target);

        Assert.Multiple(() =>
        {
            Assert.That(isFound, Is.False);
            Assert.That(target, Is.EqualTo(AiString.Empty));
            Assert.That(target.IsEmpty, Is.True);
        });
    }

    [Test]
    public void ReportsAPlainMissForANameItDoesNotHold()
    {
        // Each read misses on a store that has had a name written to it, so its index of names and kinds exists. What
        // makes the read a plain miss rather than a mismatch is that the index holds nothing under the name. The stub
        // store records every mismatch it is told of and the warning store warns of each, so a mismatch would show in
        // the stub's record or the warning store's sink, and both stay empty.
        DataStoreBase[] dataStores = { _dataStore, _warningStore };

        Assert.Multiple(() =>
        {
            foreach (DataStoreBase dataStore in dataStores)
            {
                dataStore.SetFloat("Health", 0.5f);
                Assert.That(dataStore.TryGetString("Side", out AiString _), Is.False,
                    "a name never written, beside one that was");

                dataStore.SetInt("AmmoCount", 7);
                dataStore.Remove("AmmoCount");
                Assert.That(dataStore.TryGetInt("AmmoCount", out int _), Is.False, "a name removed");

                dataStore.Clear();
                Assert.That(dataStore.TryGetFloat("Health", out float _), Is.False, "a name forgotten by Clear");
            }

            Assert.That(_dataStore.TypeMismatches, Is.Empty);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void KeepsTheMostRecentValueWrittenUnderAName()
    {
        _dataStore.SetFloat("Health", 0.5f);
        _dataStore.SetFloat("Health", 0.25f);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetFloat("Health", out float health), Is.True);
            Assert.That(health, Is.EqualTo(0.25f));
        });
    }

    [Test]
    public void KeepsEveryNameWrittenAsTheSameKind()
    {
        // Each kind's table is made on the first write of that kind, so a second name is what shows a later write adds
        // to that table rather than replacing it.
        Assert.Multiple(() =>
        {
            foreach (DataStoreValueType valueType in ValueTypes)
            {
                AiString firstName = $"First{valueType}";
                AiString secondName = $"Second{valueType}";

                Write(_dataStore, firstName, valueType);
                Write(_dataStore, secondName, valueType);

                Assert.That(TryRead(_dataStore, firstName, valueType), Is.True, firstName.Text);
                Assert.That(TryRead(_dataStore, secondName, valueType), Is.True, secondName.Text);
            }
        });
    }

    [Test]
    public void MatchesNamesWithoutRegardToCase()
    {
        // An AiString is hashed from its lower cased text, so matching ignores case without a comparer.
        _dataStore.SetString("Side", "Enemy");

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetString("side", out AiString side), Is.True);
            Assert.That(side.Text, Is.EqualTo("Enemy"));

            Assert.That(_dataStore.Contains("SIDE"), Is.True);
        });
    }

    [Test]
    public void ReportsWhichKindOfValueANameHolds()
    {
        _dataStore.SetFloat("Health", 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetValueType("Health", out DataStoreValueType valueType), Is.True);
            Assert.That(valueType, Is.EqualTo(DataStoreValueType.Float));

            Assert.That(_dataStore.TryGetValueType("Side", out DataStoreValueType _), Is.False);
        });
    }

    [Test]
    public void ReportsTheKindOfANameOfEveryKind()
    {
        Assert.Multiple(() =>
        {
            foreach (DataStoreValueType valueType in ValueTypes)
            {
                AiString name = valueType.ToString();

                Write(_dataStore, name, valueType);

                Assert.That(_dataStore.Contains(name), Is.True, name.Text);
                Assert.That(_dataStore.TryGetValueType(name, out DataStoreValueType reportedValueType), Is.True,
                    name.Text);
                Assert.That(reportedValueType, Is.EqualTo(valueType), name.Text);
            }
        });
    }

    [Test]
    public void EnumeratesEveryNameItHoldsWithTheKindItWasWrittenAs()
    {
        _dataStore.SetBool("IsOfficer", true);
        _dataStore.SetFloat("Health", 0.5f);
        _dataStore.SetString("Side", "Enemy");

        Assert.That(ListEntries(), Is.EquivalentTo(new[] { "IsOfficer Bool", "Health Float", "Side String" }));
    }

    [Test]
    public void EnumeratesNothingWhenNothingHasBeenWritten()
    {
        Assert.That(ListEntries(), Is.Empty);
    }

    [Test]
    public void ReportsAMismatchWhenANameIsReadAsTheWrongKind()
    {
        _dataStore.SetFloat("Health", 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetString("Health", out AiString _), Is.False);
            Assert.That(_dataStore.TypeMismatches, Is.EqualTo(new[] { "Health Float String" }));
        });
    }

    [Test]
    public void ReportsAMissAndAWarningWhenANameIsReadAsAnyOtherKind()
    {
        // A store warns about a name once, so each pair of kinds is read under a name of its own.
        List<string> expectedWarnings = new();

        Assert.Multiple(() =>
        {
            foreach (DataStoreValueType writtenValueType in ValueTypes)
            {
                foreach (DataStoreValueType readValueType in ValueTypes)
                {
                    if (readValueType == writtenValueType)
                    {
                        continue;
                    }

                    AiString name = $"{writtenValueType}ReadAs{readValueType}";

                    Write(_warningStore, name, writtenValueType);

                    Assert.That(TryRead(_warningStore, name, readValueType), Is.False, name.Text);
                    expectedWarnings.Add(
                        $"Warning Guard the {name.Text} name was written as {writtenValueType} and used as {readValueType}");
                }
            }

            Assert.That(_logSink.Entries, Is.EqualTo(expectedWarnings));
        });
    }

    [Test]
    public void ReplacesTheValueWhenANameIsWrittenAsADifferentKind()
    {
        _dataStore.SetFloat("Health", 0.5f);
        _dataStore.SetString("Health", "Wounded");

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.TryGetValueType("Health", out DataStoreValueType valueType), Is.True);
            Assert.That(valueType, Is.EqualTo(DataStoreValueType.String));

            // The float has to be gone. Otherwise the name would hold a value in two typed tables at once, with the
            // recorded value type pointing at only one of them.
            Assert.That(_dataStore.TryGetFloat("Health", out float _), Is.False);

            Assert.That(_dataStore.TypeMismatches, Is.EqualTo(new[] { "Health Float String", "Health String Float" }));
        });
    }

    [Test]
    public void ForgetsANameThatWasRemoved()
    {
        _dataStore.SetFloat("Health", 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.Remove("Health"), Is.True);
            Assert.That(_dataStore.Contains("Health"), Is.False);
            Assert.That(_dataStore.TryGetFloat("Health", out float _), Is.False);

            // The name is gone rather than left behind holding a stale kind.
            Assert.That(_dataStore.TypeMismatches, Is.Empty);
        });
    }

    [Test]
    public void ForgetsANameOfEveryKindThatWasRemoved()
    {
        // A write of another kind evicts the old value the way Remove does, so each kind is checked after a Remove and
        // after a write of another kind.
        Assert.Multiple(() =>
        {
            foreach (DataStoreValueType valueType in ValueTypes)
            {
                AiString removedName = $"Removed{valueType}";
                AiString rewrittenName = $"Rewritten{valueType}";
                DataStoreValueType otherValueType = valueType == DataStoreValueType.Bool
                    ? DataStoreValueType.Int
                    : DataStoreValueType.Bool;

                Write(_dataStore, removedName, valueType);
                _dataStore.Remove(removedName);

                Write(_dataStore, rewrittenName, valueType);
                Write(_dataStore, rewrittenName, otherValueType);

                Assert.That(TryRead(_dataStore, removedName, valueType), Is.False, removedName.Text);
                Assert.That(TryRead(_dataStore, rewrittenName, valueType), Is.False, rewrittenName.Text);
            }
        });
    }

    [Test]
    public void StopsEnumeratingANameThatWasRemoved()
    {
        _dataStore.SetFloat("Health", 0.5f);
        _dataStore.SetInt("AmmoCount", 7);

        _dataStore.Remove("Health");

        Assert.That(ListEntries(), Is.EqualTo(new[] { "AmmoCount Int" }));
    }

    [Test]
    public void ReportsThatANameItNeverHeldCouldNotBeRemoved()
    {
        Assert.That(_dataStore.Remove("Health"), Is.False);
    }

    [Test]
    public void ForgetsEveryNameWhenCleared()
    {
        _dataStore.SetBool("IsOfficer", true);
        _dataStore.SetEntity("SnipeTarget", _guard);

        _dataStore.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_dataStore.Contains("IsOfficer"), Is.False);
            Assert.That(_dataStore.Contains("SnipeTarget"), Is.False);

            _dataStore.SetFloat("SnipeTarget", 1f);
            Assert.That(_dataStore.TypeMismatches, Is.Empty);
        });
    }

    [Test]
    public void ForgetsEveryValueOfEveryKindWhenCleared()
    {
        // A pooled store is cleared and handed to its next character, which must read back nothing the last one wrote.
        foreach (DataStoreValueType valueType in ValueTypes)
        {
            Write(_dataStore, valueType.ToString(), valueType);
        }

        _dataStore.Clear();

        Assert.Multiple(() =>
        {
            foreach (DataStoreValueType valueType in ValueTypes)
            {
                Assert.That(TryRead(_dataStore, valueType.ToString(), valueType), Is.False, valueType.ToString());
            }
        });
    }

    [Test]
    public void AllocatesNoTablesUntilSomethingIsWritten()
    {
        // The bound sits far below the cost of nine eager dictionaries and comfortably above the bare instance, so the
        // test fails if the tables move back to construction. A high-churn entity, such as a Threat per bullet impact,
        // is what the lazy tables exist for.
        const int storeCount = 1000;
        const long bytesPerStoreBound = 200;

        _ = new StubDataStore();

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int index = 0; index < storeCount; index++)
        {
            _ = new StubDataStore();
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

        Assert.That(allocatedBytes, Is.LessThan(storeCount * bytesPerStoreBound));
    }

    [Test]
    public void WarnsExactlyOnceWhenANameIsAskedForAsAnotherKind()
    {
        _warningStore.SetInt("Ammo", 3);

        Assert.Multiple(() =>
        {
            Assert.That(_warningStore.TryGetFloat("Ammo", out _), Is.False);
            Assert.That(_warningStore.TryGetFloat("Ammo", out _), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Warning Guard the Ammo name was written as Int and used as Float",
            }), "each name warns once");
        });
    }

    [Test]
    public void WarnsWhenAWriteChangesANamesKind()
    {
        _warningStore.SetInt("Ammo", 3);
        _warningStore.SetFloat("Ammo", 0.5f);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            "Warning Guard the Ammo name was written as Int and used as Float",
        }));
    }

    [Test]
    public void WarnsAgainAboutAMismatchedNameAfterBeingCleared()
    {
        // A pooled store is cleared for its next character, whose mismatch under the same name is a new one.
        _warningStore.SetInt("Ammo", 3);
        _warningStore.TryGetFloat("Ammo", out _);

        _warningStore.Clear();

        _warningStore.SetInt("Ammo", 3);
        _warningStore.TryGetFloat("Ammo", out _);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            "Warning Guard the Ammo name was written as Int and used as Float",
            "Warning Guard the Ammo name was written as Int and used as Float",
        }));
    }

    [Test]
    public void WarnsAboutAMismatchMetWhileItsSinkFilteredWarningsOnceTheSinkShowsThem()
    {
        // A host debugging its game lowers its sink's threshold to see the warnings it had been filtering. The mismatch
        // met while they were filtered was never reported, so it is still owed its one warning.
        ThresholdLogSink logSink = new() { Threshold = LogSeverity.Error };
        BrainBlackboard store = new()
        {
            LogSink = logSink,
            LogChannel = "Guard",
        };

        store.SetInt("Ammo", 3);
        store.TryGetFloat("Ammo", out _);

        logSink.Threshold = LogSeverity.Warning;
        store.TryGetFloat("Ammo", out _);
        store.TryGetFloat("Ammo", out _);

        Assert.That(logSink.Entries, Is.EqualTo(new[]
        {
            "Warning Guard the Ammo name was written as Int and used as Float",
        }));
    }

    /// <summary>Walks the fixture's store and describes each entry as its name and the kind it reports.</summary>
    /// <remarks>
    ///     A dictionary decides the order its names come out in, which the store does not promise, so the tests that hold
    ///     several entries compare these as a set.
    /// </remarks>
    private List<string> ListEntries()
    {
        List<string> entries = new();

        foreach (DataStoreEntry entry in _dataStore.Entries)
        {
            entries.Add($"{entry.Name.Text} {entry.ValueType}");
        }

        return entries;
    }

    /// <summary>Writes a value of the given kind under the name, the same value for every name of that kind.</summary>
    private void Write(DataStoreBase dataStore, AiString name, DataStoreValueType valueType)
    {
        switch (valueType)
        {
            case DataStoreValueType.Bool:
                dataStore.SetBool(name, true);
                break;
            case DataStoreValueType.Int:
                dataStore.SetInt(name, 7);
                break;
            case DataStoreValueType.Float:
                dataStore.SetFloat(name, 0.5f);
                break;
            case DataStoreValueType.String:
                dataStore.SetString(name, "Enemy");
                break;
            case DataStoreValueType.Vector3:
                dataStore.SetVector3(name, Vector3.Zero);
                break;
            case DataStoreValueType.Rotation:
                dataStore.SetRotation(name, Rotation.Identity);
                break;
            case DataStoreValueType.Entity:
                dataStore.SetEntity(name, _guard);
                break;
            case DataStoreValueType.EntityList:
                List<EntityBase> targets = new() { _guard };
                dataStore.SetEntityList(name, targets);
                break;
        }
    }

    /// <summary>Reads the name through the getter of the given kind and reports whether the read found a value.</summary>
    private static bool TryRead(DataStoreBase dataStore, AiString name, DataStoreValueType valueType)
    {
        return valueType switch
        {
            DataStoreValueType.Bool => dataStore.TryGetBool(name, out _),
            DataStoreValueType.Int => dataStore.TryGetInt(name, out _),
            DataStoreValueType.Float => dataStore.TryGetFloat(name, out _),
            DataStoreValueType.String => dataStore.TryGetString(name, out _),
            DataStoreValueType.Vector3 => dataStore.TryGetVector3(name, out _),
            DataStoreValueType.Rotation => dataStore.TryGetRotation(name, out _),
            DataStoreValueType.Entity => dataStore.TryGetEntity(name, out _),
            _ => dataStore.TryGetEntityList(name, out _),
        };
    }

    /// <summary>A sink that drops every entry below its threshold, as a console filtering by severity does.</summary>
    private sealed class ThresholdLogSink : ILogSink
    {
        private readonly List<string> _entries = new();

        /// <summary>The least severe entry the sink shows.</summary>
        public LogSeverity Threshold { get; set; }

        /// <summary>The entries shown, each one line of severity, channel, and message.</summary>
        public IReadOnlyList<string> Entries => _entries;

        /// <summary>True for a severity at or above the threshold.</summary>
        public bool IsEnabled(LogSeverity severity)
        {
            return severity >= Threshold;
        }

        /// <summary>Records the entry if its severity is at or above the threshold, and drops it otherwise.</summary>
        public void Write(LogSeverity severity, AiString channel, string message)
        {
            if (IsEnabled(severity))
            {
                _entries.Add($"{severity} {channel.Text} {message}");
            }
        }
    }
}
