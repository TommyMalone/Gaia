using Gaia.Core.Blackboards;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Entities;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Covers a guard whose <see cref="DualUtilityReasoner" /> uses
///     <see cref="ExecutionHistoryConsideration.RankRetentionFactor" /> to see an investigation through.
/// </summary>
/// <remarks>
///     <para>
///         The guard fights at rank 10 when a threat exists, investigates a noise or an alarm at rank 5 while either is
///         recent, and patrols at rank 0 otherwise. The reason to investigate fades a few seconds after the noise, so
///         without retention the guard turns back halfway to it. Retaining part of the rank keeps the walk going, while
///         still letting an alarm of the same rank, or a threat of a higher one, interrupt. Retaining all of it raises the
///         walk back to the alarm's own rank, where the alarm can tie the walk but no longer outranks it.
///     </para>
///     <para>
///         The spec stands in for the game's sensors, writing how long ago the noise and the alarm happened onto the
///         <see cref="BrainBlackboard" /> before each cycle. The investigate action reports
///         <see cref="RecordAction.IsDone" /> when the spec calls <see cref="RecordAction.SetDone" />.
///     </para>
/// </remarks>
/// <seealso>
///     Kevin Dill, Eugene Ray Pursel, Pat Garrity and Gino Fragomeni, "Design Patterns for the Configuration of
///     Utility-Based AI", I/ITSEC, 2012
/// </seealso>
[TestFixture]
public class GuardSpec
{
    [SetUp]
    public void BuildGuard()
    {
        _timeSource = new ManualTimeSource();
        _executed = new List<string>();
        _threats = new List<EntityBase>();

        _noiseHeardTime = null;
        _alarmHeardTime = null;

        _brain = new Brain();
        _brain.Blackboard.SetEntityList("Threats", _threats);

        _guard = new Actor
        {
            Name = "Guard",
            Brain = _brain,
        };

        _investigateAction = new RecordAction("Investigate", _executed, false);

        _investigateHistory = new ExecutionHistoryConsideration
        {
            TimeSource = _timeSource,
            FinishedWeightFunction = VetoWhenFinished(),
        };

        _reasoner = new DualUtilityReasoner { RandomSource = new FixedRandomSource(0f) };
        _reasoner.AddOption(BuildFightOption());
        _reasoner.AddOption(BuildInvestigateOption());
        _reasoner.AddOption(BuildAlarmOption());
        _reasoner.AddOption(BuildPatrolOption());

        _brain.RootReasoner = _reasoner;
        _brain.Enable();
    }

    private static readonly Seconds OneCycleTime = new(1f);
    private static readonly Seconds StaysRecentTime = new(3f);

    private ManualTimeSource _timeSource;
    private List<string> _executed;
    private List<EntityBase> _threats;

    /// <summary>When the noise happened, or null while none has.</summary>
    private Seconds? _noiseHeardTime;

    /// <summary>When the alarm happened, or null while none has.</summary>
    private Seconds? _alarmHeardTime;

    private Brain _brain;
    private Actor _guard;

    private RecordAction _investigateAction;
    private ExecutionHistoryConsideration _investigateHistory;

    private DualUtilityReasoner _reasoner;

    [Test]
    public void PatrolsWhenNothingHasHappened()
    {
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[] { "Patrol.Select", "Patrol.Update" }));
    }

    [Test]
    public void WalksTowardANoiseItHasJustHeard()
    {
        HearNoise();

        Cycle();

        Assert.That(_executed, Is.EqualTo(new[] { "Investigate.Select", "Investigate.Update" }));
    }

    [Test]
    public void TurnsBackWhenTheNoiseFadesWithNoRankRetained()
    {
        // The reason to investigate is gone before the guard gets there, and nothing holds the option in its tier.
        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Deselect", "Patrol.Select", "Patrol.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void KeepsWalkingAfterTheNoiseFadesWithSomeRankRetained()
    {
        // The Inertia pattern. Six tenths of rank 5 is 3, which still beats patrolling at 0.
        _investigateHistory.RankRetentionFactor = 0.6f;

        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void GoesBackToPatrollingOnceItArrives()
    {
        // Arriving sets the action done, the FinishedWeightFunction veto ends the option, and with the noise long
        // faded there is nothing left to investigate.
        _investigateHistory.RankRetentionFactor = 0.6f;

        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        _investigateAction.SetDone(true);
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Update",
            "Investigate.Deselect", "Patrol.Select", "Patrol.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void StillDropsTheWalkForAnAlarmWithSomeRankRetained()
    {
        // An alarm is a fresh reason at rank 5, which beats the 3 the walk retained.
        _investigateHistory.RankRetentionFactor = 0.6f;

        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        HearAlarm();
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Update",
            "Investigate.Deselect", "Alarm.Select", "Alarm.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void IgnoresAnAlarmWithAllOfItsRankRetained()
    {
        // The Commit pattern. The walk keeps the whole rank 5, so a fresh alarm only ties it. The reasoner settles a
        // tie with its weighted random pick, and the fixture pins that roll at 0, which always lands on the first
        // option still in the running. That is the walk, added before the alarm.
        _investigateHistory.RankRetentionFactor = 1f;

        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        HearAlarm();
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Update",
            "Investigate.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void AlwaysDropsTheWalkForAThreat()
    {
        // Rank 10 sits above anything retention can keep.
        _investigateHistory.RankRetentionFactor = 1f;

        HearNoise();
        Cycle();

        StubEntity intruder = new("Intruder");
        _threats.Add(intruder);
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Deselect", "Fight.Select", "Fight.Update",
            // @formatter:on
        }));
    }

    private StubOption BuildFightOption()
    {
        WeightValues rankTen = new(0f, 1f, 10f);
        BooleanWeightFunction rankTenWhenFound = new(rankTen, WeightValues.Vetoed);

        EntityExistsConsideration threatExists = new()
        {
            DataStore = _brain.Blackboard,
            VariableName = "Threats",
            WeightFunction = rankTenWhenFound,
        };

        RecordAction fight = new("Fight", _executed, false);

        return new StubOption([threatExists], [fight]);
    }

    private StubOption BuildInvestigateOption()
    {
        FloatVariableConsideration noiseIsRecent = RecentEvent("SecondsSinceNoise");

        return new StubOption([noiseIsRecent, _investigateHistory], [_investigateAction]);
    }

    private StubOption BuildAlarmOption()
    {
        FloatVariableConsideration alarmIsRecent = RecentEvent("SecondsSinceAlarm");
        RecordAction alarm = new("Alarm", _executed, false);

        return new StubOption([alarmIsRecent], [alarm]);
    }

    private StubOption BuildPatrolOption()
    {
        WeightValues rankZero = new(1f, 1f, 0f);
        TuningConsideration alwaysRankZero = new(rankZero);
        RecordAction patrol = new("Patrol", _executed, false);

        return new StubOption([alwaysRankZero], [patrol]);
    }

    /// <summary>Rank 5 while the named event happened recently, nothing once it has faded, and vetoed if it never happened.</summary>
    private FloatVariableConsideration RecentEvent(string secondsSinceVariable)
    {
        WeightValues rankFive = new(0f, 1f, 5f);
        FloatSequenceWeightFunction recentOnly = new(WeightValues.Neutral);
        FloatSequenceEntry recent = new(StaysRecentTime.Value, rankFive);
        recentOnly.AddEntry(recent);

        return new FloatVariableConsideration
        {
            DataStore = _brain.Blackboard,
            VariableName = secondsSinceVariable,
            WeightFunction = recentOnly,
            MissingVariableWeights = WeightValues.Vetoed,
        };
    }

    private static ConstantWeightFunction VetoWhenFinished()
    {
        return new ConstantWeightFunction { Weights = WeightValues.Vetoed };
    }

    private void HearNoise()
    {
        _noiseHeardTime = _timeSource.CurrentTime;
    }

    private void HearAlarm()
    {
        _alarmHeardTime = _timeSource.CurrentTime;
    }

    /// <summary>Runs a cycle one second on from the previous one.</summary>
    private void Cycle()
    {
        Cycle(OneCycleTime);
    }

    /// <summary>Advances the clock, writes how long ago each event happened as a sensor would, and runs one cycle.</summary>
    private void Cycle(Seconds delta)
    {
        _timeSource.Advance(delta);

        Sense("SecondsSinceNoise", _noiseHeardTime);
        Sense("SecondsSinceAlarm", _alarmHeardTime);

        _guard.Update();
    }

    /// <summary>Writes how long ago the event happened onto the blackboard, or nothing if it never happened.</summary>
    private void Sense(string secondsSinceVariable, Seconds? heardTime)
    {
        if (heardTime.HasValue)
        {
            Seconds timeSince = _timeSource.CurrentTime - heardTime.Value;
            _brain.Blackboard.SetFloat(secondsSinceVariable, timeSince.Value);
        }
    }
}
