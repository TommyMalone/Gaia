using Gaia.Core.Considerations;
using Gaia.Core.Services.Time;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers ExecutionHistoryConsideration, which reports different values depending on whether its option is
///     running now, has run before, or has never run.
/// </summary>
/// <remarks>
///     Each of the three cases has a weight function of its own, so the spec asserts which one answered rather than
///     inferring it from the values. The clock is a ManualTimeSource, so every elapsed time is exact.
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

        _whileRunningWeights = new WeightValues(1f, 1f, float.MinValue);
        _neverRunWeights = new WeightValues(2f, 1f, float.MinValue);
        _sinceLastRunWeights = new WeightValues(3f, 1f, float.MinValue);

        _whileRunningWeightFunction = new RecordingWeightFunction { Result = _whileRunningWeights };
        _neverRunWeightFunction = new RecordingWeightFunction { Result = _neverRunWeights };
        _sinceLastRunWeightFunction = new RecordingWeightFunction { Result = _sinceLastRunWeights };

        _consideration = new ExecutionHistoryConsideration
        {
            TimeSource = _timeSource,
            WhileRunningWeightFunction = _whileRunningWeightFunction,
            NeverRunWeightFunction = _neverRunWeightFunction,
            SinceLastRunWeightFunction = _sinceLastRunWeightFunction,
        };
    }

    private ManualTimeSource _timeSource;

    private WeightValues _whileRunningWeights;
    private WeightValues _neverRunWeights;
    private WeightValues _sinceLastRunWeights;

    private RecordingWeightFunction _whileRunningWeightFunction;
    private RecordingWeightFunction _neverRunWeightFunction;
    private RecordingWeightFunction _sinceLastRunWeightFunction;

    private ExecutionHistoryConsideration _consideration;

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

        Seconds runningTime = new(5f);
        _timeSource.Advance(runningTime);

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_whileRunningWeights));
    }

    [Test]
    public void HandsTheTimeSinceSelectionToTheWhileRunningFunction()
    {
        _consideration.Select();

        Seconds runningTime = new(5f);
        _timeSource.Advance(runningTime);

        _consideration.Calculate();

        Assert.That(_whileRunningWeightFunction.LastValue, Is.EqualTo(5f));
    }

    [Test]
    public void ReportsTheSinceLastRunValuesAfterItsOptionHasRun()
    {
        _consideration.Select();

        Seconds runningTime = new(5f);
        _timeSource.Advance(runningTime);

        _consideration.Deselect();

        Seconds cooldownTime = new(3f);
        _timeSource.Advance(cooldownTime);

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

        Seconds runningTime = new(5f);
        _timeSource.Advance(runningTime);

        _consideration.Deselect();

        Seconds cooldownTime = new(3f);
        _timeSource.Advance(cooldownTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastValue, Is.EqualTo(cooldownTime.Value));
    }

    [Test]
    public void ReportsAGrowingElapsedTimeAcrossCycles()
    {
        _consideration.Select();
        _consideration.Deselect();

        Seconds firstCycleTime = new(3f);
        _timeSource.Advance(firstCycleTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastValue, Is.EqualTo(3f));

        Seconds secondCycleTime = new(4f);
        _timeSource.Advance(secondCycleTime);

        _consideration.Calculate();

        Assert.That(_sinceLastRunWeightFunction.LastValue, Is.EqualTo(7f));
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
        Assert.That(_sinceLastRunWeightFunction.LastValue, Is.EqualTo(1f));
    }

    [Test]
    public void PassesSelectionAlongToEveryWeightFunction()
    {
        // Any of the three can be one that rolls fresh values, so all of them hear about selection.
        _consideration.Select();
        _consideration.Deselect();

        Assert.Multiple(() =>
        {
            Assert.That(_whileRunningWeightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_neverRunWeightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_sinceLastRunWeightFunction.SelectCount, Is.EqualTo(1));

            Assert.That(_whileRunningWeightFunction.DeselectCount, Is.EqualTo(1));
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
}
