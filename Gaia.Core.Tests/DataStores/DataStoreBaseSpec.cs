using Gaia.Core.Blackboards;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
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
            }), "the report survives every build and each name warns once");
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
}
