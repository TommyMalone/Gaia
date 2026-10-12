using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.DataStores;
using Gaia.Core.Entities;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Proves a steady-state decision cycle allocates nothing, which is what keeps hundreds of agents from producing
///     collector work every cycle. A walk of a data store's entries and a region's two answers are held to the same
///     standard.
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
///         A warm-up runs first so tiered compilation and any lazy framework work happen outside the measured window. It
///         also outlasts <see cref="MonitorSettings.DoneWarningWindow" />, since the steady-state case's selected option
///         holds no actions and so is done from its first cycle, and the monitor builds that warning once for the
///         reasoner's default sink. The measurement is thread-local allocated bytes across a thousand cycles.
///     </para>
///     <para>
///         The transition case exists because a DualUtilityReasoner rolls every cycle, so a change of selection is
///         routine across the brains a host runs rather than an exception. The transition path builds its selection and
///         action lines only for a sink enabled at Message severity, so the case pins allocation-free transitions only
///         under a sink that filters, since the shipped <see cref="TraceLogSink" /> enables every severity and builds the
///         strings.
///     </para>
///     <para>
///         The store case walks a store holding four entries of four kinds. A walk of a data store is not part of a
///         decision cycle, and it is held to the same standard because a debug reader walking every store a host keeps
///         would otherwise allocate once per walk. It runs the same warm-up and the same thousand measured passes, and
///         counts the entries the measured passes read, so a walk that reads nothing cannot pass as one that allocates
///         nothing.
///     </para>
///     <para>
///         The region case asks a hull that follows a ByName target whether a position is inside it and for a random
///         position, a thousand times after the same warm-up. A sample guesses until one lands behind every plane, so the
///         measured passes run the rejection loop, the walk of the planes and the read of the cached bounds. They also run
///         the target's walk of its list and the move of the hull's transform to the target's position. The case counts
///         the answers that land inside, so a region that answers nothing cannot pass as one that allocates nothing.
///     </para>
///     <para>
///         The group case calculates a group whose rule reads what it holds by index, a thousand times after the same
///         warm-up, and counts what the rule read, so a rule that read nothing cannot pass as one that allocates nothing.
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

    /// <summary>Wraps the reasoner in a monitored, enabled brain, the configuration whose cycle the fixture measures.</summary>
    private static Brain BuildMonitoredBrain(ReasonerBase reasoner)
    {
        Brain brain = new(reasoner) { Monitor = new BrainMonitor(new MonitorSettings()) };
        brain.Enable();
        return brain;
    }

    /// <summary>Runs the warm-up, then measures the thread-local bytes a thousand further walks of a store allocate.</summary>
    /// <param name="store">The store to walk.</param>
    /// <param name="entriesRead">How many entries the measured walks read, which the failure message reports.</param>
    private static long MeasureEntryWalks(DataStoreBase store, out int entriesRead)
    {
        entriesRead = 0;

        for (int pass = 0; pass < WarmUpCycles; pass++)
        {
            foreach (DataStoreEntry entry in store.Entries)
            {
                entriesRead++;
            }
        }

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        entriesRead = 0;

        for (int pass = 0; pass < MeasuredCycles; pass++)
        {
            foreach (DataStoreEntry entry in store.Entries)
            {
                entriesRead++;
            }
        }

        return GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
    }

    /// <summary>
    ///     Runs the warm-up, then measures the thread-local bytes a thousand further passes allocate, each asking the
    ///     region whether a position is inside it and for a random position.
    /// </summary>
    /// <param name="region">The region to ask.</param>
    /// <param name="position">The position every pass asks about.</param>
    /// <param name="answersInside">
    ///     How many measured answers held the position or found a sample, which the failure message
    ///     reports.
    /// </param>
    private static long MeasureRegionQueries(RegionBase region, Vector3 position, out int answersInside)
    {
        for (int pass = 0; pass < WarmUpCycles; pass++)
        {
            region.IsInRegion(position);
            region.TryGetRandomPosition(out _);
        }

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        answersInside = 0;

        for (int pass = 0; pass < MeasuredCycles; pass++)
        {
            if (region.IsInRegion(position))
            {
                answersInside++;
            }

            if (region.TryGetRandomPosition(out _))
            {
                answersInside++;
            }
        }

        return GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
    }

    /// <summary>Runs the warm-up, then measures the thread-local bytes a thousand further calculations of a group allocate.</summary>
    /// <param name="group">The group to calculate.</param>
    /// <param name="addendsRead">
    ///     The sum of the addends its rule read in the measured calculations, which the failure message
    ///     reports.
    /// </param>
    private static long MeasureGroupCalculations(RecordingGroupConsideration group, out float addendsRead)
    {
        for (int pass = 0; pass < WarmUpCycles; pass++)
        {
            group.Calculate();
        }

        float sumBefore = group.AddendSum;
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int pass = 0; pass < MeasuredCycles; pass++)
        {
            group.Calculate();
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

        addendsRead = group.AddendSum - sumBefore;
        return allocatedBytes;
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

    [Test]
    public void WalksAWarmStoresEntriesWithoutAllocating()
    {
        const int entriesHeld = 4;

        StubDataStore store = new();
        store.SetBool("IsUnderFire", false);
        store.SetFloat("Health", 0.5f);
        store.SetInt("AmmoCount", 7);
        store.SetString("Side", "Enemy");

        long allocatedBytes = MeasureEntryWalks(store, out int entriesRead);

        Assert.That(entriesRead, Is.EqualTo(entriesHeld * MeasuredCycles), "every measured walk reads every entry");
        Assert.That(allocatedBytes, Is.EqualTo(0), $"walking {entriesRead} entries must not feed the collector");
    }

    [Test]
    public void AsksARegionThatFollowsATargetWithoutAllocating()
    {
        const int answersPerPass = 2;

        Vector3 lookoutPosition = new(50f, 0f, 50f);

        Contact lookout = new()
        {
            Name = "Lookout",
            Position = lookoutPosition,
        };

        StubDataStore store = new();
        store.SetEntityList("Contacts", [lookout]);

        ByNameTarget lookoutTarget = new()
        {
            DataStore = store,
            ListName = "Contacts",
            EntityName = "Lookout",
        };

        // A cube 2 on a side around the hull's own origin, which the target carries to the lookout.
        Vector3 towardNegativeX = new(-1f, 0f, 0f);
        Vector3 towardPositiveX = new(1f, 0f, 0f);
        Vector3 towardNegativeY = new(0f, -1f, 0f);
        Vector3 towardPositiveY = new(0f, 1f, 0f);
        Vector3 towardNegativeZ = new(0f, 0f, -1f);
        Vector3 towardPositiveZ = new(0f, 0f, 1f);

        Plane leftWall = new(towardNegativeX, 1f);
        Plane rightWall = new(towardPositiveX, 1f);
        Plane floor = new(towardNegativeY, 1f);
        Plane ceiling = new(towardPositiveY, 1f);
        Plane nearWall = new(towardNegativeZ, 1f);
        Plane farWall = new(towardPositiveZ, 1f);

        ConvexHullRegion hull = new()
        {
            OriginTarget = lookoutTarget,
            RandomSource = new Xoshiro128RandomSource(7),
        };
        hull.AddPlane(leftWall);
        hull.AddPlane(rightWall);
        hull.AddPlane(floor);
        hull.AddPlane(ceiling);
        hull.AddPlane(nearWall);
        hull.AddPlane(farWall);

        long allocatedBytes = MeasureRegionQueries(hull, lookoutPosition, out int answersInside);

        Assert.That(answersInside, Is.EqualTo(answersPerPass * MeasuredCycles),
            "every measured pass holds the lookout's position and finds a sample");
        Assert.That(allocatedBytes, Is.EqualTo(0),
            $"answering {answersInside} questions of a following region must not feed the collector");
    }

    [Test]
    public void CombinesAGroupWithoutAllocating()
    {
        const float addendsPerCalculation = 3f;

        StubConsideration first = new(new WeightValues(1f, 1f, 0f));
        StubConsideration second = new(new WeightValues(2f, 1f, 0f));
        RecordingGroupConsideration group = new([first, second]);

        long allocatedBytes = MeasureGroupCalculations(group, out float addendsRead);

        Assert.That(addendsRead, Is.EqualTo(addendsPerCalculation * MeasuredCycles),
            "every measured calculation reads both addends by index");
        Assert.That(allocatedBytes, Is.EqualTo(0),
            $"combining a group whose rule read {addendsRead} in addends must not feed the collector");
    }
}
