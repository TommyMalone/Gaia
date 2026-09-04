using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Services.Time;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers which of <see cref="ExecutionHistoryConsideration" />'s four weight functions answers in each state,
///     and the configuration patterns built from them.
/// </summary>
/// <remarks>
///     <para>
///         Each case has a weight function of its own, so the spec asserts which one answered rather than inferring it
///         from the values. The clock is a <see cref="ManualTimeSource" />, so every elapsed time is exact.
///     </para>
///     <para>
///         The cited paper names the configurations these functions build, Is Done, Commit, Inertia, Cooldown and Do
///         Once. The tests demonstrating them double as the manual, one pattern per test.
///     </para>
/// </remarks>
/// <seealso>
///     Kevin Dill, Eugene Ray Pursel, Pat Garrity and Gino Fragomeni, "Design Patterns for the Configuration of
///     Utility-Based AI", I/ITSEC, 2012
/// </seealso>
[TestFixture]
public class ExecutionHistoryConsiderationSpec
{
    [SetUp]
    public void BuildConsideration()
    {
        _timeSource = new ManualTimeSource();
        _logSink = new RecordingLogSink();

        _whileRunningWeights = new WeightValues(1f, 1f, float.MinValue);
        _finishedWeights = new WeightValues(2f, 1f, float.MinValue);
        _neverRunWeights = new WeightValues(3f, 1f, float.MinValue);
        _sinceLastRunWeights = new WeightValues(4f, 1f, float.MinValue);

        _whileRunningWeightFunction = new RecordingWeightFunction { Result = _whileRunningWeights };
        _finishedWeightFunction = new RecordingWeightFunction { Result = _finishedWeights };
        _neverRunWeightFunction = new RecordingWeightFunction { Result = _neverRunWeights };
        _sinceLastRunWeightFunction = new RecordingWeightFunction { Result = _sinceLastRunWeights };

        _consideration = new ExecutionHistoryConsideration
        {
            TimeSource = _timeSource,
            WhileRunningWeightFunction = _whileRunningWeightFunction,
            FinishedWeightFunction = _finishedWeightFunction,
            NeverRunWeightFunction = _neverRunWeightFunction,
            SinceLastRunWeightFunction = _sinceLastRunWeightFunction,
        };

        // The option the consideration belongs to, whose one action is still running until a test says otherwise.
        // A second consideration gives the option a rank, so there is something for RankRetentionFactor to keep.
        _optionRank = 10f;
        WeightValues rankWeights = new(0f, 1f, _optionRank);
        _rankConsideration = new StubConsideration(rankWeights);

        _executed = new List<string>();
        _action = new RecordAction("Action", _executed, false);
        _option = new StubOption([_consideration, _rankConsideration], [_action]);
    }

    private static readonly Seconds RunningTime = new(5f);
    private static readonly Seconds CooldownTime = new(3f);

    private ManualTimeSource _timeSource;
    private RecordingLogSink _logSink;

    private WeightValues _whileRunningWeights;
    private WeightValues _finishedWeights;
    private WeightValues _neverRunWeights;
    private WeightValues _sinceLastRunWeights;

    private RecordingWeightFunction _whileRunningWeightFunction;
    private RecordingWeightFunction _finishedWeightFunction;
    private RecordingWeightFunction _neverRunWeightFunction;
    private RecordingWeightFunction _sinceLastRunWeightFunction;

    private ExecutionHistoryConsideration _consideration;

    private float _optionRank;
    private StubConsideration _rankConsideration;

    private List<string> _executed;
    private RecordAction _action;
    private StubOption _option;

    [Test]
    public void ReportsTheFinishedValuesOnceItsOptionsActionsAreDone()
    {
        // The Is Done pattern. A FinishedWeightFunction that vetoes is how an option ends itself, since the reasoner
        // takes no notice of IsDone.
        SelectTheOption();
        _action.SetDone(true);

        _timeSource.Advance(RunningTime);

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_consideration.Weights, Is.EqualTo(_finishedWeights));
            Assert.That(_finishedWeightFunction.LastFloat, Is.EqualTo(RunningTime.Value));
            Assert.That(_whileRunningWeightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReportsTheWhileRunningValuesUntilTheActionsAreDone()
    {
        SelectTheOption();

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));

        _action.SetDone(true);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_finishedWeights));
    }

    [Test]
    public void KeepsReportingTheWhileRunningValuesWithNoFinishedFunction()
    {
        // An option with no FinishedWeightFunction keeps running after its actions are done.
        _consideration.FinishedWeightFunction = null;

        SelectTheOption();
        _action.SetDone(true);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));
    }

    [Test]
    public void RecordsTheRankItsOptionWasSelectedAt()
    {
        SelectTheOption();

        Assert.That(_consideration.RankWhenLastSelected, Is.EqualTo(_optionRank));
    }

    [Test]
    public void KeepsNoneOfTheSelectedRankByDefault()
    {
        SelectTheOption();

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));
    }

    [Test]
    public void KeepsAPortionOfTheSelectedRankWhileRunning()
    {
        // The Inertia pattern. Half the rank holds off near equals but not something far more important.
        _consideration.RankRetentionFactor = 0.5f;

        SelectTheOption();

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_consideration.Weights.Rank, Is.EqualTo(5f));
            Assert.That(_consideration.Weights.Addend, Is.EqualTo(_whileRunningWeights.Addend));
            Assert.That(_consideration.Weights.Multiplier, Is.EqualTo(_whileRunningWeights.Multiplier));
        });
    }

    [Test]
    public void KeepsTheWholeSelectedRankToCommit()
    {
        // The Commit pattern. Nothing of the same rank or lower can interrupt the option while it runs.
        _consideration.RankRetentionFactor = 1f;

        SelectTheOption();

        _consideration.Calculate();

        Assert.That(_consideration.Weights.Rank, Is.EqualTo(_optionRank));
    }

    [Test]
    public void LeavesAHigherRankFromTheWhileRunningFunctionAlone()
    {
        WeightValues higherRank = new(1f, 1f, 20f);
        _whileRunningWeightFunction.Result = higherRank;
        _consideration.RankRetentionFactor = 0.5f;

        SelectTheOption();

        _consideration.Calculate();

        Assert.That(_consideration.Weights.Rank, Is.EqualTo(20f));
    }

    [Test]
    public void RetainsNothingFromAnOptionWithNoRank()
    {
        // A portion of a rank at or below zero would be higher than the rank itself, so nothing is kept.
        _rankConsideration.Set(WeightValues.Neutral);
        _consideration.RankRetentionFactor = 1f;

        SelectTheOption();

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));
    }

    [Test]
    public void StopsRetainingRankOnceTheActionsAreFinished()
    {
        // RankRetentionFactor sees the actions through. Once IsDone is true, FinishedWeightFunction alone decides.
        _consideration.RankRetentionFactor = 1f;

        SelectTheOption();
        _action.SetDone(true);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_finishedWeights));
    }

    [Test]
    public void ReportsTheNeverRunValuesBeforeItsOptionHasRun()
    {
        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_neverRunWeights));
    }

    [Test]
    public void ReportsTheWhileRunningValuesWhileItsOptionIsSelected()
    {
        _consideration.Select();

        _timeSource.Advance(RunningTime);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));
    }

    [Test]
    public void HandsTheTimeSinceSelectionToTheWhileRunningFunction()
    {
        _consideration.Select();

        _timeSource.Advance(RunningTime);

        _consideration.Calculate();

        Assert.That(_whileRunningWeightFunction.LastFloat, Is.EqualTo(RunningTime.Value));
    }

    [Test]
    public void ReportsTheSinceLastRunValuesAfterItsOptionHasRun()
    {
        _consideration.Select();

        _timeSource.Advance(RunningTime);

        _consideration.Deselect();

        _timeSource.Advance(CooldownTime);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_sinceLastRunWeights));
    }

    [Test]
    public void HandsTheTimeSinceDeselectionToTheSinceLastRunFunction()
    {
        // The Cooldown pattern's clock. A SinceLastRunWeightFunction that vetoes for a period is Cooldown, and one
        // that vetoes forever is Do Once. Either way the time runs from when the option stopped rather than from when
        // it started.
        _consideration.Select();

        _timeSource.Advance(RunningTime);

        _consideration.Deselect();

        _timeSource.Advance(CooldownTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastFloat, Is.EqualTo(CooldownTime.Value));
    }

    [Test]
    public void ReportsAGrowingElapsedTimeAcrossCycles()
    {
        _consideration.Select();
        _consideration.Deselect();

        Seconds firstCycleTime = new(3f);
        _timeSource.Advance(firstCycleTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastFloat, Is.EqualTo(3f));

        Seconds secondCycleTime = new(4f);
        _timeSource.Advance(secondCycleTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastFloat, Is.EqualTo(7f));
    }

    [Test]
    public void ConsultsOnlyTheFunctionForItsCurrentState()
    {
        _consideration.Select();

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_whileRunningWeightFunction.CalculationCount, Is.EqualTo(1));
            Assert.That(_neverRunWeightFunction.CalculationCount, Is.EqualTo(0));
            Assert.That(_sinceLastRunWeightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void StartsItsHistoryOverWhenSelectedAgain()
    {
        _consideration.Select();
        _consideration.Deselect();

        Seconds longIdleTime = new(100f);
        _timeSource.Advance(longIdleTime);

        _consideration.Select();

        Seconds secondRunTime = new(2f);
        _timeSource.Advance(secondRunTime);

        _consideration.Deselect();

        Seconds sinceSecondRunTime = new(1f);
        _timeSource.Advance(sinceSecondRunTime);

        _consideration.Calculate();

        // The cooldown counts from the most recent run, not the first one.
        Assert.That(_sinceLastRunWeightFunction.LastFloat, Is.EqualTo(sinceSecondRunTime.Value));
    }

    [Test]
    public void PassesSelectionAlongToEveryWeightFunction()
    {
        // Any of the four can be one that rolls fresh values, so selection reaches all of them.
        _consideration.Select();
        _consideration.Deselect();

        Assert.Multiple(() =>
        {
            Assert.That(_whileRunningWeightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_finishedWeightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_neverRunWeightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_sinceLastRunWeightFunction.SelectCount, Is.EqualTo(1));

            Assert.That(_whileRunningWeightFunction.DeselectCount, Is.EqualTo(1));
            Assert.That(_finishedWeightFunction.DeselectCount, Is.EqualTo(1));
            Assert.That(_neverRunWeightFunction.DeselectCount, Is.EqualTo(1));
            Assert.That(_sinceLastRunWeightFunction.DeselectCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReportsNeutralValuesForAStateWithNoWeightFunction()
    {
        ExecutionHistoryConsideration consideration = new() { TimeSource = _timeSource };

        consideration.Calculate();

        Assert.That(consideration.Weights, Is.EqualTo(WeightValues.Neutral));
    }

    /// <summary>
    ///     Calculates the option before selecting it, the order a reasoner's update follows, so that the rank it is
    ///     selected at is the one its considerations report.
    /// </summary>
    private void SelectTheOption()
    {
        _option.Calculate();
        _option.Select();
    }

    [Test]
    public void LoadsItsRoleWeightFunctionsAndRetention()
    {
        const string snippet = """
                               <Consideration Type="ExecutionHistory" RankRetentionFactor="0.5">
                                   <WhileRunning Type="Constant" Addend="1"/>
                                   <Finished Type="Constant" Multiplier="0"/>
                                   <NeverRun Type="Constant" Addend="2"/>
                                   <SinceLastRun Type="FloatSequence">
                                       <Entry Threshold="5" Multiplier="0"/>
                                   </SinceLastRun>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ExecutionHistoryConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.RankRetentionFactor, Is.EqualTo(0.5f));
            Assert.That(loaded.WhileRunningWeightFunction, Is.InstanceOf<ConstantWeightFunction>());
            Assert.That(loaded.FinishedWeightFunction, Is.InstanceOf<ConstantWeightFunction>());
            Assert.That(loaded.NeverRunWeightFunction, Is.InstanceOf<ConstantWeightFunction>());
            Assert.That(loaded.SinceLastRunWeightFunction, Is.InstanceOf<FloatSequenceWeightFunction>());
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }
}
