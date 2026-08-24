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
///     Covers the sniper from the chapter's running example, a whole brain assembled from real reasoners,
///     considerations, weight functions, regions and targets working together.
/// </summary>
/// <remarks>
///     <para>
///         The sniper waits until there are enemies in its kill zone, then takes a shot as long as its line of retreat
///         is clear and it is not under attack. Under attack it retreats if it can and fights back if it cannot. A
///         <see cref="RuleBasedReasoner" /> holds those four options in priority order, and the Snipe option runs a
///         <see cref="SequenceReasoner" /> of set pose, raise weapon, a <see cref="PauseAction" /> and fire.
///     </para>
///     <para>
///         There is no picker yet, so the spec stands in for the game's sensors. Before each cycle it sorts the contacts
///         the sniper knows about into the kill zone list and the retreat route list by asking the two regions, which is
///         the work a picker would do from inside the <see cref="EntityExistsConsideration" /> instances. The concrete
///         actions are <see cref="RecordAction" /> instances, so what is asserted is the order things were selected in.
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
        _contacts = new List<EntityBase>();

        _sniper = new Actor
        {
            Name = "Sniper",
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

        _brain = new Brain();
        _sniper.Brain = _brain;

        List<EntityBase> killZoneContacts = new();
        List<EntityBase> retreatRouteContacts = new();

        _brain.Blackboard.SetBool("IsUnderFire", false);
        _brain.Blackboard.SetEntityList("KillZoneContacts", killZoneContacts);
        _brain.Blackboard.SetEntityList("RetreatRouteContacts", retreatRouteContacts);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(BuildRetreatOption());
        reasoner.AddOption(BuildFightBackOption());
        reasoner.AddOption(BuildSnipeOption());
        reasoner.AddOption(BuildHideOption());

        _brain.RootReasoner = reasoner;
        _brain.Enable();
    }

    private static readonly Seconds OneCycleTime = new(1f);
    private static readonly Seconds CooldownMinTime = new(60f);
    private static readonly Seconds CooldownMaxTime = new(120f);
    private static readonly Seconds AimTime = new(2f);

    private ManualTimeSource _timeSource;
    private List<string> _executed;
    private List<EntityBase> _contacts;

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
        Vector3 inKillZone = new(10f, 0f, 140f);

        Cycle();

        AddContact("Patrol", inKillZone);

        // One cycle each for set pose and raise weapon, one to start the aim pause, and a final cycle that advances
        // the clock by AimTime so the pause completes and the sequence fires.
        Cycle();
        Cycle();
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
        Vector3 inKillZone = new(10f, 0f, 140f);
        Vector3 onRetreatRoute = new(0f, 0f, -50f);

        AddContact("Patrol", inKillZone);
        AddContact("Flanker", onRetreatRoute);

        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
    }

    [Test]
    public void RetreatsWhenUnderFireWithAClearLineOfRetreat()
    {
        Vector3 inKillZone = new(10f, 0f, 140f);
        AddContact("Patrol", inKillZone);
        Cycle();

        _brain.Blackboard.SetBool("IsUnderFire", true);
        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "SetPose", "Retreat" }));
    }

    [Test]
    public void FightsBackWhenUnderFireWithTheLineOfRetreatBlocked()
    {
        Vector3 onRetreatRoute = new(0f, 0f, -50f);
        AddContact("Flanker", onRetreatRoute);
        _brain.Blackboard.SetBool("IsUnderFire", true);

        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "FightBack" }));
    }

    [Test]
    public void WaitsOutTheCooldownBeforeTheNextShot()
    {
        Vector3 inKillZone = new(10f, 0f, 140f);
        Vector3 alsoInKillZone = new(-10f, 0f, 160f);

        StubEntity patrol = AddContact("Patrol", inKillZone);

        Cycle();
        Cycle();
        Cycle();
        Cycle(AimTime);

        Assert.That(CountSelections("Fire"), Is.EqualTo(1));

        // The shot lands. With nothing left in the kill zone the sniper hides, and the cooldown starts.
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
        // A shot every minute or two as long as there are still enemies in the marketplace. Once the sequence has
        // fired, the Snipe option reports IsDone and its ExecutionHistoryConsideration vetoes it through
        // FinishedWeightFunction. Deselection starts the SinceLastRunWeightFunction cooldown, and the sniper hides
        // until it has passed.
        Vector3 inKillZone = new(10f, 0f, 140f);
        AddContact("Patrol", inKillZone);

        Cycle();
        Cycle();
        Cycle();
        Cycle(AimTime);

        // One cycle for the SequenceReasoner to notice Fire IsDone, and one for ShotCooldown to see Snipe IsDone.
        Cycle();
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
        // The sniper has relocated well outside the range band its Snipe option allows.
        Vector3 farBehindTheKillZone = new(0f, 10f, -300f);
        Vector3 inKillZone = new(10f, 0f, 140f);

        _sniper.Position = farBehindTheKillZone;
        AddContact("Patrol", inKillZone);

        Cycle();

        Assert.That(Selections(), Is.EqualTo(new[] { "Hide" }));
    }

    private StubOption BuildRetreatOption()
    {
        RecordAction retreat = new("Retreat", _executed, false);

        return new StubOption([UnderFire(), LineOfRetreatClear()], [retreat]);
    }

    private StubOption BuildFightBackOption()
    {
        RecordAction fightBack = new("FightBack", _executed, false);

        return new StubOption([UnderFire()], [fightBack]);
    }

    private StubOption BuildSnipeOption()
    {
        RecordAction setPose = new("SetPose", _executed);
        RecordAction raiseWeapon = new("RaiseWeapon", _executed);
        PauseAction aim = new(AimTime) { TimeSource = _timeSource };
        RecordAction fire = new("Fire", _executed);

        StubOption setPoseStep = new([], [setPose]);
        StubOption raiseWeaponStep = new([], [raiseWeapon]);
        StubOption aimStep = new([], [aim]);
        StubOption fireStep = new([], [fire]);

        SequenceReasoner shot = new();
        shot.AddOption(setPoseStep);
        shot.AddOption(raiseWeaponStep);
        shot.AddOption(aimStep);
        shot.AddOption(fireStep);

        SubreasonerAction takeTheShot = new(shot);

        return new StubOption([LineOfRetreatClear(), TargetInKillZone(), KillZoneInRange(), ShotCooldown()],
            [takeTheShot]);
    }

    private StubOption BuildHideOption()
    {
        RecordAction hide = new("Hide", _executed, false);

        return new StubOption([], [hide]);
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
        PositionTarget killZoneCenter = new() { Position = _killZone.Center };

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
        FloatSequenceEntry stillCoolingDown = new(CooldownMinTime.Value, CooldownMaxTime.Value, WeightValues.Vetoed);
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
        _brain.Blackboard.TryGetEntityList("KillZoneContacts", out List<EntityBase>? killZoneContacts);
        _brain.Blackboard.TryGetEntityList("RetreatRouteContacts", out List<EntityBase>? retreatRouteContacts);

        killZoneContacts!.Clear();
        retreatRouteContacts!.Clear();

        foreach (EntityBase contact in _contacts)
        {
            if (!contact.IsValid)
            {
                continue;
            }

            if (_killZone.IsInRegion(contact.Position))
            {
                killZoneContacts.Add(contact);
            }

            if (_retreatRoute.IsInRegion(contact.Position))
            {
                retreatRouteContacts.Add(contact);
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
