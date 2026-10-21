using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Entities;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Covers the sniper from the chapter's running example, the library's reasoners, considerations, weight
///     functions, regions and targets working together as a whole brain, with test helpers for the options, the contacts
///     and the game's own actions.
/// </summary>
/// <remarks>
///     <para>
///         The sniper waits until there are enemies in its kill zone, then takes a shot as long as its line of retreat
///         is clear and it is not under attack. Under attack it retreats if it can and fights back if it cannot. A
///         <see cref="RuleBasedReasoner" /> holds those four options in priority order, and the Snipe option runs a
///         <see cref="SequenceReasoner" /> of set pose, raise weapon, a <see cref="PauseAction" /> and fire.
///     </para>
///     <para>
///         Sensing belongs to the game, so the spec stands in for its sensors. Before each cycle it sorts the contacts
///         the sniper knows about into the kill zone list and the retreat route list by asking the two regions, and the
///         <see cref="EntityExistsConsideration" /> instances read those lists. The concrete actions are
///         <see cref="RecordAction" /> instances, so what is asserted is the order things were selected in.
///     </para>
/// </remarks>
/// <seealso>"Modular AI", Section 8.3.3</seealso>
[TestFixture]
public class SniperSpec
{
    [SetUp]
    public void BuildSniper()
    {
        _timeSource = new ManualTimeSource();
        _executed = new List<string>();
        _logSink = new RecordingLogSink();
        _recordActionFactory = new RecordActionFactory(_executed, _logSink, CharacterName);
        _contacts = new List<EntityBase>();

        _sniper = new Actor
        {
            Name = CharacterName,
            Position = new Vector3(0f, 10f, 0f),
        };

        // The kill zone is 150 meters out. The line of retreat runs the other way.
        _killZone = new CircleRegion
        {
            Origin = new Vector3(0f, 0f, 150f),
            Radius = 40f,
        };
        _retreatRoute = new RectangleRegion
        {
            Origin = new Vector3(0f, 0f, -105f),
            Extents = new Vector2(5f, 95f),
        };

        _brain = new Brain
        {
            LogSink = _logSink,
            LogChannel = CharacterName,
        };
        _sniper.Brain = _brain;

        _killZoneContacts = new List<EntityBase>();
        _retreatRouteContacts = new List<EntityBase>();

        _brain.Blackboard.SetBool("IsUnderFire", false);
        _brain.Blackboard.SetEntityList("KillZoneContacts", _killZoneContacts);
        _brain.Blackboard.SetEntityList("RetreatRouteContacts", _retreatRouteContacts);

        RuleBasedReasoner reasoner = new()
        {
            LogSink = _logSink,
            LogChannel = CharacterName,
        };
        reasoner.AddOption(BuildRetreatOption());
        reasoner.AddOption(BuildFightBackOption());
        reasoner.AddOption(BuildSnipeOption());
        reasoner.AddOption(BuildHideOption());

        _brain.RootReasoner = reasoner;
        _brain.Enable();
    }

    private const string CharacterName = "Sniper";

    private static readonly Vector3 InKillZone = new(10f, 0f, 140f);
    private static readonly Vector3 OnRetreatRoute = new(0f, 0f, -50f);

    private static readonly Seconds OneCycleTime = new(1f);
    private static readonly Seconds CooldownMinTime = new(60f);
    private static readonly Seconds CooldownMaxTime = new(120f);
    private static readonly Seconds AimTime = new(2f);

    private ManualTimeSource _timeSource;
    private List<string> _executed;
    private RecordingLogSink _logSink;
    private RecordActionFactory _recordActionFactory;
    private List<EntityBase> _contacts;
    private List<EntityBase> _killZoneContacts;
    private List<EntityBase> _retreatRouteContacts;

    private Actor _sniper;
    private Brain _brain;

    private CircleRegion _killZone;
    private RectangleRegion _retreatRoute;

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
    public void IgnoresAnEnemyOutsideTheKillZone()
    {
        Vector3 beyondKillZone = new(0f, 0f, 300f);
        AddContact("Patrol", beyondKillZone);

        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
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
    public void WaitsOutTheCooldownBeforeTheNextShot()
    {
        Vector3 alsoInKillZone = new(-10f, 0f, 160f);

        StubEntity patrol = AddContact("Patrol", InKillZone);

        Cycle();
        Cycle(AimTime);

        Assert.That(CountSelections("Fire"), Is.EqualTo(1));

        // The target is marked invalid, so Sense drops it from the kill zone list. With nothing left in the kill zone
        // the sniper hides, and the cooldown starts.
        patrol.IsValid = false;
        Cycle();

        AddContact("Reinforcement", alsoInKillZone);

        Seconds withinTheCooldownTime = new(30f);
        Cycle(withinTheCooldownTime);

        Assert.That(CountSelections("SetPose"), Is.EqualTo(1), "thirty seconds is inside the cooldown");

        Seconds beyondTheCooldownTime = new(61f);
        Cycle(beyondTheCooldownTime);

        Assert.That(CountSelections("SetPose"), Is.EqualTo(2), "ninety one seconds is past it");
    }

    [Test]
    public void TakesAnotherShotAfterTheCooldownWithTheTargetStillStanding()
    {
        // A shot every minute or two as long as there are still enemies in the kill zone. Once the sequence has fired,
        // the Snipe option reports IsDone and its ExecutionHistoryConsideration vetoes it through
        // FinishedWeightFunction. Deselection starts the SinceLastRunWeightFunction cooldown, and the sniper hides
        // until it has passed.
        AddContact("Patrol", InKillZone);

        Cycle();
        Cycle(AimTime);

        // The sequence finished in the cycle it fired, so one more cycle is all ShotCooldown needs to see Snipe
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
    public void HidesWhenTheKillZoneIsOutOfRange()
    {
        // The sniper has relocated well outside the range band its Snipe option allows.
        Vector3 farBehindTheKillZone = new(0f, 10f, -300f);

        _sniper.Position = farBehindTheKillZone;
        AddContact("Patrol", InKillZone);

        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
    }

    [Test]
    public void ReportsEveryDecisionToItsLogSink()
    {
        // The same shot TakesAShotWhenAnEnemyEntersTheKillZone covers, read from the sink instead of the executed
        // list. Both levels of the tree write to the same sink, so the sequence's steps appear underneath the Snipe
        // option that started them, which is what a nested reasoner looks like from outside.

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

    private StubOption BuildRetreatOption()
    {
        RecordAction retreat = _recordActionFactory.Build("Retreat", false);

        return new StubOption([UnderFire(), LineOfRetreatClear()], [retreat]) { Name = "Retreat" };
    }

    private StubOption BuildFightBackOption()
    {
        RecordAction fightBack = _recordActionFactory.Build("FightBack", false);

        return new StubOption([UnderFire()], [fightBack]) { Name = "FightBack" };
    }

    private StubOption BuildSnipeOption()
    {
        RecordAction setPose = _recordActionFactory.Build("SetPose");
        RecordAction raiseWeapon = _recordActionFactory.Build("RaiseWeapon");
        RecordAction fire = _recordActionFactory.Build("Fire");

        PauseAction aim = new(AimTime)
        {
            TimeSource = _timeSource,
            Name = "Aim",
            LogSink = _logSink,
            LogChannel = CharacterName,
        };

        StubOption setPoseStep = new([], [setPose]) { Name = "SetPoseStep" };
        StubOption raiseWeaponStep = new([], [raiseWeapon]) { Name = "RaiseWeaponStep" };
        StubOption aimStep = new([], [aim]) { Name = "AimStep" };
        StubOption fireStep = new([], [fire]) { Name = "FireStep" };

        SequenceReasoner shot = new()
        {
            LogSink = _logSink,
            LogChannel = CharacterName,
        };
        shot.AddOption(setPoseStep);
        shot.AddOption(raiseWeaponStep);
        shot.AddOption(aimStep);
        shot.AddOption(fireStep);

        SubreasonerAction takeTheShot = new(shot)
        {
            Name = "TakeTheShot",
            LogSink = _logSink,
            LogChannel = CharacterName,
        };

        return new StubOption([LineOfRetreatClear(), TargetInKillZone(), KillZoneInRange(), ShotCooldown()],
            [takeTheShot]) { Name = "Snipe" };
    }

    private StubOption BuildHideOption()
    {
        RecordAction hide = _recordActionFactory.Build("Hide", false);

        return new StubOption([], [hide]) { Name = "Hide" };
    }

    /// <summary>Vetoes unless the game has flagged the sniper as under fire.</summary>
    private BooleanVariableConsideration UnderFire()
    {
        BooleanWeightFunction allowWhenTrue = new(WeightValues.Neutral, WeightValues.Vetoed);

        return new BooleanVariableConsideration
        {
            DataStore = _brain.Blackboard,
            VariableName = "IsUnderFire",
            WeightFunction = allowWhenTrue,
        };
    }

    /// <summary>Vetoes while any contact stands on the retreat route.</summary>
    private EntityExistsConsideration LineOfRetreatClear()
    {
        BooleanWeightFunction vetoWhenFound = new(WeightValues.Vetoed, WeightValues.Neutral);

        return new EntityExistsConsideration
        {
            DataStore = _brain.Blackboard,
            VariableName = "RetreatRouteContacts",
            WeightFunction = vetoWhenFound,
        };
    }

    /// <summary>Vetoes until some contact is in the kill zone.</summary>
    private EntityExistsConsideration TargetInKillZone()
    {
        BooleanWeightFunction vetoWhenEmpty = new(WeightValues.Neutral, WeightValues.Vetoed);

        return new EntityExistsConsideration
        {
            DataStore = _brain.Blackboard,
            VariableName = "KillZoneContacts",
            WeightFunction = vetoWhenEmpty,
        };
    }

    /// <summary>Vetoes unless the kill zone is between 50 and 300 meters from the sniper.</summary>
    private DistanceConsideration KillZoneInRange()
    {
        FloatSequenceWeightFunction rangeBand = new(WeightValues.Vetoed);
        FloatSequenceEntry tooClose = new(50f, WeightValues.Vetoed);
        FloatSequenceEntry inRange = new(300f, WeightValues.Neutral);
        rangeBand.AddEntry(tooClose);
        rangeBand.AddEntry(inRange);

        SelfTarget self = new() { Self = _sniper };
        PositionTarget killZoneCenter = new() { Position = _killZone.Origin };

        return new DistanceConsideration
        {
            FromTarget = self,
            ToTarget = killZoneCenter,
            WeightFunction = rangeBand,
        };
    }

    /// <summary>Ends the shot once the sequence has fired, and vetoes for a minute or two after, rolled each time.</summary>
    private ExecutionHistoryConsideration ShotCooldown()
    {
        // The shot ends itself. Without this the Snipe option would stay selected after firing and never cool down.
        ConstantWeightFunction vetoWhenFinished = new() { Weights = WeightValues.Vetoed };

        // A roll of 0.5 puts every cooldown at 90 seconds, which keeps the spec's timings exact.
        FixedRandomSource halfwayRoll = new(0.5f);
        FloatSequenceWeightFunction cooldown = new(WeightValues.Neutral) { RandomSource = halfwayRoll };
        FloatSequenceEntry stillCoolingDown =
            new((float)CooldownMinTime.Value, (float)CooldownMaxTime.Value, WeightValues.Vetoed);
        cooldown.AddEntry(stillCoolingDown);

        return new ExecutionHistoryConsideration
        {
            TimeSource = _timeSource,
            FinishedWeightFunction = vetoWhenFinished,
            SinceLastRunWeightFunction = cooldown,
        };
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
