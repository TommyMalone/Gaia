using Gaia.Core.Brains;
using Gaia.Core.Factories;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Covers the reproducibility contract end to end. The same seed, the same clock, the same configuration and the
///     same load order produce the same recorded decisions, cycle for cycle, at every level of the tree.
/// </summary>
/// <remarks>
///     <para>
///         Every run gets a loader, a seeded source, a manual clock and monitor settings of its own, and the runs that
///         should match are compared monitor for monitor through <see cref="BrainMonitor.ReasonerMonitors" />. With
///         monitor settings, the loader attaches a brain monitor to each brain it loads, and that monitor gives every
///         reasoner in the tree a monitor of its own, so the nested reasoner's rolls are compared too, where a monitor set
///         by hand on the root reasoner would record the root's rolls alone.
///     </para>
///     <para>
///         The brain rolls every cycle, a dual utility reasoner choosing by weight between two tuned options, and the
///         lighter of them runs a nested dual utility reasoner of its own. Both draw from the one stream the load derived
///         for the brain, interleaved, so a draw that one run makes and the other does not offsets every later roll in
///         that run and separates the histories within a few cycles. A change to the generator itself alters every run
///         alike, and <see cref="Services.Random.Xoshiro128RandomSourceSpec" /> catches that instead.
///     </para>
/// </remarks>
[TestFixture]
public class DeterminismSpec
{
    private const int Cycles = 100;

    private const int SharedSeed = 99;

    private static readonly Seconds CycleTime = new(1f / 60f);

    private const string Snippet = """
                                   <Brain>
                                       <Reasoner Type="DualUtility">
                                           <Option Type="ConsiderationAndAction" Name="Graze">
                                               <Consideration Type="Tuning" Addend="3"/>
                                           </Option>
                                           <Option Type="ConsiderationAndAction" Name="Wander">
                                               <Consideration Type="Tuning" Addend="1"/>
                                               <Action Type="Subreasoner" Name="PickADirection">
                                                   <Reasoner Type="DualUtility">
                                                       <Option Type="ConsiderationAndAction" Name="Left">
                                                           <Consideration Type="Tuning" Addend="1"/>
                                                       </Option>
                                                       <Option Type="ConsiderationAndAction" Name="Right">
                                                           <Consideration Type="Tuning" Addend="1"/>
                                                       </Option>
                                                   </Reasoner>
                                               </Action>
                                           </Option>
                                       </Reasoner>
                                   </Brain>
                                   """;

    [Test]
    public void RecordsTheSameDecisionsForTheSameSeedClockAndConfiguration()
    {
        BrainMonitor firstRun = RunOnce();
        BrainMonitor secondRun = RunOnce();

        // The nested reasoner runs only on the cycles its option is selected, so the fixture states what it covers
        // rather than leaving a silent pass when the nested history is empty.
        Assert.That(firstRun.ReasonerMonitors, Has.Count.EqualTo(2), "the walk reached the nested reasoner");
        Assert.That(firstRun.ReasonerMonitors[1].Path, Is.EqualTo("Wander"));
        Assert.That(firstRun.ReasonerMonitors[1].History.CycleCount, Is.GreaterThan(0),
            "the nested reasoner ran, so its rolls are under test");

        AssertRunsMatch(firstRun, secondRun);
    }

    [Test]
    public void ReproducesAWholeFleetAndKeepsItsBrainsApart()
    {
        (BrainMonitor firstRunA, BrainMonitor firstRunB) = RunFleetOnce();
        (BrainMonitor secondRunA, BrainMonitor secondRunB) = RunFleetOnce();
        BrainMonitor aloneA = RunOnce();

        DecisionHistory firstA = firstRunA.ReasonerMonitors[0].History;
        DecisionHistory firstB = firstRunB.ReasonerMonitors[0].History;
        DecisionHistory alone = aloneA.ReasonerMonitors[0].History;

        bool brainsDiverge = false;

        for (int cyclesBack = 0; cyclesBack < firstA.CycleCount && !brainsDiverge; cyclesBack++)
        {
            brainsDiverge = !firstA.GetCycleFromNewest(cyclesBack).Roll
                .Equals(firstB.GetCycleFromNewest(cyclesBack).Roll);
        }

        AssertRunsMatch(firstRunA, secondRunA);
        AssertRunsMatch(firstRunB, secondRunB);

        Assert.Multiple(() =>
        {
            for (int cyclesBack = 0; cyclesBack < firstA.CycleCount; cyclesBack++)
            {
                // Two brains drawing in turn from one shared stream would differ from each other too, so the divergence
                // asserted below does not show that the first brain has a stream of its own. The comparison with the
                // run without a second brain does, since a stream shared with the second brain would change the first
                // brain's rolls.
                Assert.That(firstA.GetCycleFromNewest(cyclesBack).Roll,
                    Is.EqualTo(alone.GetCycleFromNewest(cyclesBack).Roll),
                    $"loading a second brain changed the first brain's rolls at cyclesBack {cyclesBack}");
            }

            Assert.That(brainsDiverge, Is.True, "two brains from one loader do not roll alike");
        });
    }

    /// <summary>Asserts the two runs recorded the same decisions, reasoner for reasoner and cycle for cycle.</summary>
    /// <remarks>
    ///     The structural counts are asserted before the contents, so a run that built a different tree or recorded a
    ///     different number of cycles says so rather than failing somewhere inside a comparison.
    /// </remarks>
    private static void AssertRunsMatch(BrainMonitor first, BrainMonitor second)
    {
        Assert.That(first.ReasonerMonitors, Has.Count.EqualTo(second.ReasonerMonitors.Count),
            "the two runs built different trees");

        for (int index = 0; index < first.ReasonerMonitors.Count; index++)
        {
            ReasonerMonitor firstMonitor = first.ReasonerMonitors[index];
            ReasonerMonitor secondMonitor = second.ReasonerMonitors[index];
            DecisionHistory firstHistory = firstMonitor.History;
            DecisionHistory secondHistory = secondMonitor.History;
            string where = firstMonitor.Path.Length == 0 ? "the root reasoner" : firstMonitor.Path;

            Assert.That(firstHistory.CycleCount, Is.EqualTo(secondHistory.CycleCount),
                $"{where} recorded a different number of cycles");

            Assert.Multiple(() =>
            {
                for (int cyclesBack = 0; cyclesBack < firstHistory.CycleCount; cyclesBack++)
                {
                    DecisionCycle firstCycle = firstHistory.GetCycleFromNewest(cyclesBack);
                    DecisionCycle secondCycle = secondHistory.GetCycleFromNewest(cyclesBack);

                    Assert.That(firstCycle.CycleIndex, Is.EqualTo(secondCycle.CycleIndex));
                    Assert.That(firstCycle.Roll, Is.EqualTo(secondCycle.Roll),
                        $"{where} rolls diverged at cyclesBack {cyclesBack}");
                    Assert.That(firstCycle.SelectedName, Is.EqualTo(secondCycle.SelectedName),
                        $"{where} selections diverged at cyclesBack {cyclesBack}");
                    Assert.That(firstCycle.RecordCount, Is.EqualTo(secondCycle.RecordCount));

                    for (int optionIndex = 0; optionIndex < firstCycle.RecordCount; optionIndex++)
                    {
                        DecisionRecord firstRecord = firstHistory.GetRecordFromNewest(cyclesBack, optionIndex);
                        DecisionRecord secondRecord = secondHistory.GetRecordFromNewest(cyclesBack, optionIndex);

                        Assert.That(firstRecord.OptionName, Is.EqualTo(secondRecord.OptionName));
                        Assert.That(firstRecord.Rank, Is.EqualTo(secondRecord.Rank));
                        Assert.That(firstRecord.Weight, Is.EqualTo(secondRecord.Weight));
                        Assert.That(firstRecord.PassedScreening, Is.EqualTo(secondRecord.PassedScreening));
                    }
                }
            });
        }
    }

    /// <summary>
    ///     Loads the snippet through a new loader, clock, seeded source and sink, runs the fixed cycle count, and hands
    ///     back the brain's monitor.
    /// </summary>
    private static BrainMonitor RunOnce()
    {
        ManualTimeSource timeSource = new();
        Xoshiro128RandomSource randomSource = new(SharedSeed);
        RecordingLogSink logSink = new();

        GaiaLoader loader = new(new FactorySet(), timeSource, randomSource, logSink)
        {
            Diagnostics = new MonitorSettings(),
        };

        loader.TryLoadBrain(Snippet, "determinism.xml", out Brain? brain);

        brain!.Enable();

        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            timeSource.Advance(CycleTime);
            brain.Update();
        }

        return brain.Monitor!;
    }

    /// <summary>Loads two brains from one parsed tree and one master seed, runs both, and hands back their monitors.</summary>
    /// <remarks>
    ///     Neither brain is loaded for an actor, so neither takes a name-derived seed. Each is seeded from the master's
    ///     next value instead, which makes its stream a function of the master seed and its place in the load order. Loading
    ///     the pair again in that order reproduces both brains, and the second load leaves the first brain's rolls untouched,
    ///     because each brain runs on a source of its own seeded from the master rather than drawing from the master itself.
    /// </remarks>
    private static (BrainMonitor, BrainMonitor) RunFleetOnce()
    {
        ManualTimeSource timeSource = new();
        Xoshiro128RandomSource masterStream = new(SharedSeed);
        RecordingLogSink logSink = new();

        GaiaLoader loader = new(new FactorySet(), timeSource, masterStream, logSink)
        {
            Diagnostics = new MonitorSettings(),
        };

        loader.TryParse(Snippet, "determinism.xml", out SpecificationNode? root);

        loader.TryLoadBrain(root!, out Brain? firstBrain);
        loader.TryLoadBrain(root!, out Brain? secondBrain);

        firstBrain!.Enable();
        secondBrain!.Enable();

        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            timeSource.Advance(CycleTime);
            firstBrain.Update();
            secondBrain.Update();
        }

        return (firstBrain.Monitor!, secondBrain.Monitor!);
    }
}
