using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Proves a steady-state decision cycle allocates nothing, which is what keeps hundreds of agents from producing
///     collector work every cycle.
/// </summary>
/// <remarks>
///     <para>
///         A steady-state cycle selects the option the cycle before it selected, and a transition cycle selects a
///         different one. The fixture is a brain over a <see cref="DualUtilityReasoner" /> with a monitor attached, its
///         options scored by stub considerations with fixed weights, so the measured window covers the brain's cycle,
///         evaluation, screening, the roll and the monitor's recording. The steady-state case's selected option also
///         carries an execution history consideration whose while-running weight function is constant, so that
///         consideration's clock read and function call are inside the window. Actions are left out of the steady-state
///         case because the suite's <see cref="RecordAction" /> builds a string for every lifecycle call it records, and
///         the transition case carries a shipped PauseAction so that an action's Select and Deselect are inside its
///         window.
///     </para>
///     <para>
///         A warm-up runs first so tiered compilation and any lazy framework work happen outside the measured window.
///         The measurement is thread-local allocated bytes across a thousand cycles.
///     </para>
///     <para>
///         The transition case exists because a DualUtilityReasoner rolls every cycle, so a change of selection is
///         routine across the brains a host runs rather than an exception. The transition path builds its selection and
///         action lines only for a sink enabled at Message severity, so the case pins allocation-free transitions only
///         under a sink that filters, since the shipped <see cref="TraceLogSink" /> enables every severity and builds the
///         strings.
///     </para>
/// </remarks>
[TestFixture]
public class AllocationSpec
{
    [SetUp]
    public void BuildClock()
    {
        _timeSource = new ManualTimeSource();
    }

    private const int MeasuredCycles = 1000;
    private const int WarmUpCycles = 2000;

    private static readonly WeightValues HeavyWeight = new(3f, 1f, 0f);
    private static readonly Seconds OneCycleTime = new(1f);

    private ManualTimeSource _timeSource;

    /// <summary>Wraps the reasoner in a monitored, enabled brain, the configuration whose cycle the promise covers.</summary>
    private static Brain BuildMonitoredBrain(ReasonerBase reasoner)
    {
        Brain brain = new(reasoner) { Monitor = new BrainMonitor(new MonitorSettings()) };
        brain.Enable();
        return brain;
    }

    /// <summary>Runs the warm-up, then measures the thread-local bytes a thousand further cycles allocate.</summary>
    private long MeasureCycles(Brain brain)
    {
        for (int cycle = 0; cycle < WarmUpCycles; cycle++)
        {
            _timeSource.Advance(OneCycleTime);
            brain.Update();
        }

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int cycle = 0; cycle < MeasuredCycles; cycle++)
        {
            _timeSource.Advance(OneCycleTime);
            brain.Update();
        }

        return GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
    }

    [Test]
    public void RunsASteadyStateCycleWithoutAllocating()
    {
        WeightValues lightWeight = new(1f, 1f, 0f);

        StubConsideration firstScore = new(HeavyWeight);
        StubConsideration secondScore = new(lightWeight);

        ConstantWeightFunction neutralWhileRunning = new() { Weights = WeightValues.Neutral };
        ExecutionHistoryConsideration whileRunning = new()
        {
            TimeSource = _timeSource,
            WhileRunningWeightFunction = neutralWhileRunning,
        };

        StubOption firstOption = new([firstScore, whileRunning], []) { Name = "First" };
        StubOption secondOption = new([secondScore], []) { Name = "Second" };

        DualUtilityReasoner reasoner = new() { RandomSource = new FixedRandomSource(0f) };
        reasoner.AddOption(firstOption);
        reasoner.AddOption(secondOption);

        long allocatedBytes = MeasureCycles(BuildMonitoredBrain(reasoner));

        Assert.That(allocatedBytes, Is.EqualTo(0), "a warm decision cycle must not feed the collector");
    }

    [Test]
    public void RunsTransitioningCyclesWithoutAllocatingUnderAFilteringSink()
    {
        WeightValues comparableWeight = new(2f, 1f, 0f);

        NullLogSink filteringSink = new();
        PauseAction pauseAction = new(Seconds.Zero) { TimeSource = _timeSource, LogSink = filteringSink };

        StubOption firstOption = new([new StubConsideration(HeavyWeight)], [pauseAction]) { Name = "First" };
        StubOption secondOption = new([new StubConsideration(comparableWeight)], []) { Name = "Second" };

        DualUtilityReasoner reasoner = new()
        {
            RandomSource = new Xoshiro128RandomSource(7),
            LogSink = filteringSink,
        };
        reasoner.AddOption(firstOption);
        reasoner.AddOption(secondOption);

        long allocatedBytes = MeasureCycles(BuildMonitoredBrain(reasoner));

        Assert.That(allocatedBytes, Is.EqualTo(0),
            "a transitioning cycle with a filtering sink must not feed the collector");
    }
}
