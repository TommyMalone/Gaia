using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Covers the sniper loaded from sniper.xml, the first whole brain built from a configuration file, run through
///     the same behavior the hand-built <see cref="SniperSpec" /> asserts.
/// </summary>
/// <remarks>
///     <para>
///         The spec plays the host, through the same <see cref="GaiaLoader" /> surface a game uses. It supplies the
///         services, the actor and the sensor work exactly as <see cref="SniperSpec" /> does, registers the Record action
///         the file's Action elements name, and writes the variables the loaded considerations read onto the brain's own
///         blackboard. Everything the sniper decides with comes from the file.
///     </para>
///     <para>
///         The registration of Record on the factory set is the game-facing extension surface in use, a component the
///         library does not ship built by name from a configuration.
///     </para>
/// </remarks>
/// <seealso>"Modular AI", Section 8.3.3</seealso>
[TestFixture]
public class LoadedSniperSpec
{
    [SetUp]
    public void LoadSniper()
    {
        _timeSource = new ManualTimeSource();
        _executed = new List<string>();
        _logSink = new RecordingLogSink();
        _contacts = new List<EntityBase>();

        _sniper = new Actor
        {
            Name = CharacterName,
            Position = new Vector3(0f, 10f, 0f),
        };

        // The marketplace is 150 meters out. The line of retreat runs the other way.
        _killZone = new CircleRegion
        {
            Center = new Vector3(0f, 0f, 150f),
            Radius = 40f,
        };

        _retreatRoute = new RectangleRegion
        {
            Min = new Vector3(-5f, 0f, -200f),
            Max = new Vector3(5f, 0f, -10f),
        };

        string configurationText = File.ReadAllText(ConfigurationPath);

        FactorySet factories = new();
        factories.Actions.Register("Record", () => new RecordAction(_executed));

        // The cooldown roll is scripted through the brain's own source. The loader's master source only seeds
        // streams for brains that bring none, so the fixture hands its fixed rolls to the load itself.
        FixedRandomSource cooldownRolls = new(CooldownRoll);
        GaiaLoader loader = new(factories, _timeSource, new Xoshiro128RandomSource(1), _logSink);

        Brain? brain = null;
        _loaded = loader.TryParse(configurationText, "sniper.xml", out SpecificationNode? root) &&
                  loader.TryLoadBrain(root!, out brain, self: _sniper, randomSource: cooldownRolls);

        // A failed load yields no brain. An empty stand-in keeps the fixture alive so LoadsTheWholeBrainFromTheFile
        // can report the failure instead of every test failing in SetUp.
        _brain = brain ?? new Brain();
        _sniper.Brain = _brain;

        _killZoneContacts = new List<EntityBase>();
        _retreatRouteContacts = new List<EntityBase>();

        _brain.Blackboard.SetBool("IsUnderFire", false);
        _brain.Blackboard.SetEntityList("KillZoneContacts", _killZoneContacts);
        _brain.Blackboard.SetEntityList("RetreatRouteContacts", _retreatRouteContacts);

        _brain.Enable();
    }

    private const string CharacterName = "Sniper";

    /// <summary>A roll of 0.5 puts every cooldown at 90 seconds, which keeps the timings exact.</summary>
    private const float CooldownRoll = 0.5f;

    private static readonly string ConfigurationPath =
        Path.Combine(AppContext.BaseDirectory, "Integration", "sniper.xml");

    private static readonly Vector3 InKillZone = new(10f, 0f, 140f);
    private static readonly Vector3 OnRetreatRoute = new(0f, 0f, -50f);

    private static readonly Seconds OneCycleTime = new(1f);
    private static readonly Seconds AimTime = new(2f);

    private ManualTimeSource _timeSource;
    private List<string> _executed;
    private RecordingLogSink _logSink;
    private List<EntityBase> _contacts;
    private List<EntityBase> _killZoneContacts;
    private List<EntityBase> _retreatRouteContacts;

    private Actor _sniper;
    private Brain _brain;
    private bool _loaded;

    private CircleRegion _killZone;
    private RectangleRegion _retreatRoute;

    [Test]
    public void LoadsTheWholeBrainFromTheFile()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_loaded, Is.True);
            Assert.That(_brain.RootReasoner, Is.Not.Null);
            Assert.That(_logSink.Entries, Has.None.StartsWith("Error"), "a clean load reports nothing");
        });
    }

    [Test]
    public void HidesWhenThereIsNothingToShootAt()
    {
        Cycle();
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[] { "Hide.Select", "Hide.Update", "Hide.Update" }));
    }

    [Test]
    public void TakesAShotWhenAnEnemyEntersTheKillZone()
    {
        Cycle();
        AddContact("Patrol", InKillZone);

        // One cycle in which Snipe is selected, its two instantaneous steps pass, and the aim pause starts, then a
        // final cycle that advances the clock by AimTime so the pause completes and the sequence fires and finishes.
        Cycle();
        Cycle(AimTime);

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide", "SetPose", "RaiseWeapon", "Fire" }));
    }

    [Test]
    public void HoldsFireWhileTheLineOfRetreatIsBlocked()
    {
        AddContact("Patrol", InKillZone);
        AddContact("Flanker", OnRetreatRoute);
        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
    }

    [Test]
    public void RetreatsWhenUnderFireWithAClearLineOfRetreat()
    {
        AddContact("Patrol", InKillZone);
        Cycle();

        _brain.Blackboard.SetBool("IsUnderFire", true);
        Cycle();

        // The interrupted shot got as far as starting its aim pause, so both instantaneous steps show before the
        // retreat.
        Assert.That(Selections(), Is.EqualTo(new[] { "SetPose", "RaiseWeapon", "Retreat" }));
    }

    [Test]
    public void FightsBackWhenUnderFireWithTheLineOfRetreatBlocked()
    {
        AddContact("Flanker", OnRetreatRoute);
        _brain.Blackboard.SetBool("IsUnderFire", true);
        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "FightBack" }));
    }

    [Test]
    public void TakesAnotherShotAfterTheCooldownWithTheTargetStillStanding()
    {
        AddContact("Patrol", InKillZone);
        Cycle();
        Cycle(AimTime);

        // The sequence finished in the cycle it fired, so one more cycle is all the cooldown needs to see Snipe
        // IsDone and veto it.
        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "SetPose", "RaiseWeapon", "Fire", "Hide" }));

        Seconds almostTheCooldownTime = new(89f);
        Cycle(almostTheCooldownTime);

        Assert.That(CountSelections("SetPose"), Is.EqualTo(1), "eighty nine seconds is inside the cooldown");

        Seconds beyondTheCooldownTime = new(2f);
        Cycle(beyondTheCooldownTime);

        Assert.That(CountSelections("SetPose"), Is.EqualTo(2), "ninety one seconds is past it");
    }

    [Test]
    public void WillNotSnipeFromTooFarAway()
    {
        // The sniper has relocated well outside the range band the file's Snipe option allows.
        Vector3 farBehindTheKillZone = new(0f, 10f, -300f);

        _sniper.Position = farBehindTheKillZone;
        AddContact("Patrol", InKillZone);
        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
    }

    [Test]
    public void ReportsTheSameDecisionsTheHandBuiltSniperDoes()
    {
        // The same shot SniperSpec's log test covers, proving the loaded tree carries the stamped channel and the
        // configured names all the way down.
        Cycle();
        AddContact("Patrol", InKillZone);
        Cycle();
        Cycle(AimTime);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            // @formatter:off
            "Message Sniper brain enabled",
            "Message Sniper selected Hide", "Message Sniper Hide started",
            "Message Sniper deselected Hide", "Message Sniper Hide stopped",
            "Message Sniper selected Snipe", "Message Sniper TakeTheShot started",
            "Message Sniper selected SetPoseStep", "Message Sniper SetPose started",
            "Message Sniper deselected SetPoseStep", "Message Sniper SetPose stopped",
            "Message Sniper selected RaiseWeaponStep", "Message Sniper RaiseWeapon started",
            "Message Sniper deselected RaiseWeaponStep", "Message Sniper RaiseWeapon stopped",
            "Message Sniper selected AimStep", "Message Sniper Aim started",
            "Message Sniper deselected AimStep", "Message Sniper Aim stopped",
            "Message Sniper selected FireStep", "Message Sniper Fire started",
            "Message Sniper deselected FireStep", "Message Sniper Fire stopped",
            // @formatter:on
        }));
    }

    private StubEntity AddContact(string name, Vector3 position)
    {
        StubEntity contact = new(name) { Position = position };
        _contacts.Add(contact);
        return contact;
    }

    /// <summary>Runs a cycle one second on from the previous one.</summary>
    private void Cycle()
    {
        Cycle(OneCycleTime);
    }

    /// <summary>Advances the clock, sorts the contacts into the two regions as a sensor would, and runs one cycle.</summary>
    private void Cycle(Seconds delta)
    {
        _timeSource.Advance(delta);
        Sense();
        _sniper.Update();
    }

    /// <summary>Sorts the valid contacts into the kill zone and retreat route lists, as the game's sensors would.</summary>
    private void Sense()
    {
        _killZoneContacts.Clear();
        _retreatRouteContacts.Clear();

        foreach (EntityBase contact in _contacts)
        {
            if (!contact.IsValid)
            {
                continue;
            }

            if (_killZone.IsInRegion(contact.Position))
            {
                _killZoneContacts.Add(contact);
            }

            if (_retreatRoute.IsInRegion(contact.Position))
            {
                _retreatRouteContacts.Add(contact);
            }
        }
    }

    /// <summary>The names of the actions selected so far, in order, with updates and deselections left out.</summary>
    private List<string> Selections()
    {
        const string suffix = ".Select";
        List<string> selections = new();

        foreach (string entry in _executed)
        {
            if (entry.EndsWith(suffix))
            {
                selections.Add(entry.Substring(0, entry.Length - suffix.Length));
            }
        }

        return selections;
    }

    private int CountSelections(string actionName)
    {
        int count = 0;

        foreach (string selection in Selections())
        {
            if (selection == actionName)
            {
                count++;
            }
        }

        return count;
    }
}
