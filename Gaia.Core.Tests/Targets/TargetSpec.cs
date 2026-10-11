using Gaia.Core.Blackboards;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Targets;

/// <summary>Covers the three targets, and the distinction between having an entity and being able to find one.</summary>
/// <remarks>
///     A <see cref="ByNameTarget" /> looks its entity up in a named list on a <see cref="BrainBlackboard" />, which
///     stands in for the chapter's list of contacts. The list the fixture builds holds a stub, since nothing here depends
///     on which kind of entity is found.
/// </remarks>
/// <seealso>"Modular AI", Section 8.5.5</seealso>
[TestFixture]
public class TargetSpec
{
    [SetUp]
    public void BuildTargets()
    {
        _logSink = new RecordingLogSink();
        _blackboard = new BrainBlackboard();

        _sniperPosition = new Vector3(5f, 0f, 5f);
        _fixedPosition = new Vector3(1f, 2f, 3f);
        _lookoutPosition = new Vector3(10f, 0f, 20f);

        _sniper = new Actor
        {
            Name = "Sniper",
            Position = _sniperPosition,
        };
        _selfTarget = new SelfTarget { Self = _sniper };

        _positionTarget = new PositionTarget { Position = _fixedPosition };

        _lookout = new StubEntity("Lookout") { Position = _lookoutPosition };
        _contacts = [_lookout];
        _blackboard.SetEntityList("Contacts", _contacts);

        _byNameTarget = new ByNameTarget
        {
            DataStore = _blackboard,
            ListName = "Contacts",
            EntityName = "Lookout",
        };
    }

    private RecordingLogSink _logSink;

    private BrainBlackboard _blackboard;

    private Vector3 _sniperPosition;
    private Vector3 _fixedPosition;
    private Vector3 _lookoutPosition;

    private Actor _sniper;
    private SelfTarget _selfTarget;

    private PositionTarget _positionTarget;

    private StubEntity _lookout;
    private List<EntityBase> _contacts;

    private ByNameTarget _byNameTarget;

    [Test]
    public void ReportsTheConfiguredPositionFromAPositionTarget()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_positionTarget.TryGetPosition(out Vector3 found), Is.True);
            Assert.That(found, Is.EqualTo(_fixedPosition));
        });
    }

    [Test]
    public void NeverHasAnEntityOnAPositionTarget()
    {
        // A position is always valid and never an entity, so a component that needs an entity can tell up front that
        // this target will never supply one.
        Assert.Multiple(() =>
        {
            Assert.That(_positionTarget.HasEntity, Is.False);
            Assert.That(_positionTarget.Entity, Is.Null);
            Assert.That(_positionTarget.IsValid, Is.True);
        });
    }

    [Test]
    public void ReportsNoTurnFromAPositionTarget()
    {
        // Compared as a whole transform, so the scale of one is held as well as the identity.
        Transform unturnedAtTheFixedPosition = new(_fixedPosition, Rotation.Identity);

        Assert.Multiple(() =>
        {
            Assert.That(_positionTarget.TryGetTransform(out Transform found), Is.True);
            Assert.That(found, Is.EqualTo(unturnedAtTheFixedPosition));
        });
    }

    [Test]
    public void ReportsTheActorFromASelfTarget()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_selfTarget.HasEntity, Is.True);
            Assert.That(_selfTarget.Entity, Is.SameAs(_sniper));
            Assert.That(_selfTarget.IsValid, Is.True);
            Assert.That(_selfTarget.TryGetPosition(out Vector3 found), Is.True);
            Assert.That(found, Is.EqualTo(_sniperPosition));
        });
    }

    [Test]
    public void FollowsTheActorAsItMoves()
    {
        _sniper.Position = new Vector3(1f, 0f, 0f);
        _selfTarget.TryGetPosition(out Vector3 first);

        _sniper.Position = new Vector3(2f, 0f, 0f);
        _selfTarget.TryGetPosition(out Vector3 second);

        Assert.Multiple(() =>
        {
            Assert.That(first.X, Is.EqualTo(1f));
            Assert.That(second.X, Is.EqualTo(2f));
        });
    }

    [Test]
    public void ReportsTheActorsFacingFromASelfTarget()
    {
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        _sniper.Rotation = quarterTurnRight;

        Assert.Multiple(() =>
        {
            Assert.That(_selfTarget.TryGetTransform(out Transform found), Is.True);
            Assert.That(found.Origin, Is.EqualTo(_sniperPosition));
            Assert.That(found.Rotation, Is.EqualTo(quarterTurnRight));
        });
    }

    [Test]
    public void ReportsTheActorsScaleFromASelfTarget()
    {
        Vector3 twiceAsTall = new(1f, 2f, 1f);
        _sniper.Scale = twiceAsTall;

        Assert.Multiple(() =>
        {
            Assert.That(_selfTarget.TryGetTransform(out Transform found), Is.True);
            Assert.That(found.Scale, Is.EqualTo(twiceAsTall));
        });
    }

    [Test]
    public void IsInvalidAsASelfTargetWithNoActor()
    {
        _selfTarget.Self = null;

        Assert.Multiple(() =>
        {
            Assert.That(_selfTarget.HasEntity, Is.True);
            Assert.That(_selfTarget.IsValid, Is.False);
            Assert.That(_selfTarget.TryGetPosition(out _), Is.False);
        });
    }

    [Test]
    public void FindsAnEntityByName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.HasEntity, Is.True);
            Assert.That(_byNameTarget.IsValid, Is.True);
            Assert.That(_byNameTarget.Entity, Is.SameAs(_lookout));
            Assert.That(_byNameTarget.TryGetPosition(out Vector3 found), Is.True);
            Assert.That(found, Is.EqualTo(_lookoutPosition));
        });
    }

    [Test]
    public void ReportsTheEntitysFacingFromAByNameTarget()
    {
        Vector3 scoutPosition = new(-4f, 0f, 12f);
        Rotation quarterTurnLeft = Rotation.FromEulerAngles(0f, -90f, 0f);
        Contact scout = new()
        {
            Name = "Scout",
            Position = scoutPosition,
            Rotation = quarterTurnLeft,
        };
        _contacts.Add(scout);
        _byNameTarget.EntityName = "Scout";

        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.TryGetTransform(out Transform found), Is.True);
            Assert.That(found.Origin, Is.EqualTo(scoutPosition));
            Assert.That(found.Rotation, Is.EqualTo(quarterTurnLeft));
        });
    }

    [Test]
    public void ReportsTheEntitysScaleFromAByNameTarget()
    {
        Vector3 halfAsWide = new(0.5f, 1f, 1f);
        Contact scout = new()
        {
            Name = "Scout",
            Scale = halfAsWide,
        };
        _contacts.Add(scout);
        _byNameTarget.EntityName = "Scout";

        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.TryGetTransform(out Transform found), Is.True);
            Assert.That(found.Scale, Is.EqualTo(halfAsWide));
        });
    }

    [Test]
    public void MatchesNamesRegardlessOfCase()
    {
        _byNameTarget.EntityName = "LOOKOUT";

        Assert.That(_byNameTarget.Entity, Is.SameAs(_lookout));
    }

    [Test]
    public void StillHasAnEntityWhenItCannotFindOne()
    {
        // This is the distinction Listing 8.5 draws. The target is the kind that has an entity, and no entity by that
        // name exists right now.
        _byNameTarget.EntityName = "Officer";

        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.HasEntity, Is.True);
            Assert.That(_byNameTarget.IsValid, Is.False);
            Assert.That(_byNameTarget.Entity, Is.Null);
            Assert.That(_byNameTarget.TryGetPosition(out _), Is.False);
        });
    }

    [Test]
    public void SkipsAnEntityThatIsNoLongerValid()
    {
        _lookout.IsValid = false;

        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.IsValid, Is.False);
            Assert.That(_byNameTarget.Entity, Is.Null);
        });
    }

    [Test]
    public void LooksTheEntityUpAgainOnEveryAsk()
    {
        // An entity can leave the list or be replaced under the same name, so nothing is cached.
        Assert.That(_byNameTarget.Entity, Is.SameAs(_lookout));

        StubEntity replacement = new("Lookout");
        _contacts.Clear();
        _contacts.Add(replacement);

        Assert.That(_byNameTarget.Entity, Is.SameAs(replacement));
    }

    [Test]
    public void IsInvalidWithNoListToSearch()
    {
        _byNameTarget.ListName = "Threats";

        Assert.Multiple(() =>
        {
            Assert.That(_byNameTarget.IsValid, Is.False);
            Assert.That(_byNameTarget.TryGetPosition(out _), Is.False);
        });
    }

    [Test]
    public void IsInvalidWithNoStoreToSearch()
    {
        ByNameTarget target = new()
        {
            ListName = "Contacts",
            EntityName = "Lookout",
        };

        Assert.That(target.IsValid, Is.False);
    }

    [Test]
    public void ReportsTheSamePositionFromBothQuestions()
    {
        TargetBase[] shippedTargets = [_selfTarget, _byNameTarget, _positionTarget];

        Assert.Multiple(() =>
        {
            foreach (TargetBase target in shippedTargets)
            {
                target.TryGetPosition(out Vector3 position);
                target.TryGetTransform(out Transform transform);

                Assert.That(transform.Origin, Is.EqualTo(position), target.GetType().Name);
            }
        });
    }

    [Test]
    public void FailsBothQuestionsTogether()
    {
        _selfTarget.Self = null;
        _byNameTarget.EntityName = "Officer";

        Assert.Multiple(() =>
        {
            Assert.That(_selfTarget.TryGetPosition(out _), Is.False);
            Assert.That(_selfTarget.TryGetTransform(out _), Is.False);
            Assert.That(_byNameTarget.TryGetPosition(out _), Is.False);
            Assert.That(_byNameTarget.TryGetTransform(out _), Is.False);
        });
    }

    [Test]
    public void LoadsAFixedPositionFromItsAttribute()
    {
        const string snippet = """
                               <Target Type="Position" Position="(1, 2, 3)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        PositionTarget loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LoadsSelfFromTheCreationData()
    {
        const string snippet = """
                               <Target Type="Self"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        Actor sniper = new() { Name = "Sniper" };
        creationData.Self = sniper;

        SelfTarget loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Self, Is.SameAs(sniper));
        });
    }

    [Test]
    public void LoadsAByNameLookupWithItsStore()
    {
        const string snippet = """
                               <Target Type="ByName" List="Actors" Entity="Officer" Store="World"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        WorldBlackboard worldBlackboard = new();
        creationData.WorldBlackboard = worldBlackboard;

        AiString actorsName = "Actors";
        AiString officerName = "Officer";
        ByNameTarget loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.DataStore, Is.SameAs(worldBlackboard));
            Assert.That(loaded.ListName, Is.EqualTo(actorsName));
            Assert.That(loaded.EntityName, Is.EqualTo(officerName));
        });
    }

    [Test]
    public void RefusesAByNameLookupMissingItsList()
    {
        const string snippet = """
                               <Target Type="ByName" Entity="Officer"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ByNameTarget loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Target element has no List attribute",
            }));
        });
    }

    [Test]
    public void RefusesAByNameTargetWhoseStoreNamesNeitherBlackboard()
    {
        const string snippet = """
                               <Target Type="ByName" List="Actors" Entity="Officer" Store="Nowhere"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ByNameTarget loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Target element's Store is not Brain or World, it reads \"Nowhere\"",
            }));
        });
    }
}
