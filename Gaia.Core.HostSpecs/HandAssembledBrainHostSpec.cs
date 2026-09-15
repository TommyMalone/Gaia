using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Random;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a host can assemble a running brain in code using only the public surface. Brains, reasoners and
///     options have public constructors, while the shipped considerations, actions and weight functions are made by
///     loading rather than constructed in game code.
/// </summary>
/// <remarks>
///     The host wires the sink, the channel and a DualUtilityReasoner's random source through the same public setters
///     loading uses. The consideration and the actions the host adds are game-defined subclasses of
///     <see cref="ConsiderationBase" /> and <see cref="ActionBase" />.
/// </remarks>
[TestFixture]
public class HandAssembledBrainHostSpec
{
    [SetUp]
    public void AssembleBrain()
    {
        _selections = new List<string>();
        _logSink = new ListLogSink();
        _patrolGate = new HostGateConsideration();

        ConsiderationAndActionOption patrol = new("Patrol");
        patrol.AddConsideration(_patrolGate);
        patrol.AddAction(new HostRecordAction("Walk", _selections) { LogSink = _logSink, LogChannel = "Assembled" });

        ConsiderationAndActionOption idle = new("Idle");
        idle.AddAction(new HostRecordAction("Stand", _selections) { LogSink = _logSink, LogChannel = "Assembled" });

        RuleBasedReasoner reasoner = new()
        {
            LogSink = _logSink,
            LogChannel = "Assembled",
        };
        reasoner.AddOption(patrol);
        reasoner.AddOption(idle);

        _brain = new Brain(reasoner)
        {
            LogSink = _logSink,
            LogChannel = "Assembled",
        };
    }

    private List<string> _selections;
    private ListLogSink _logSink;
    private HostGateConsideration _patrolGate;
    private Brain _brain;

    [Test]
    public void RunsTheAssembledBrain()
    {
        _brain.Enable();
        _brain.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Walk" }), "a rule-based reasoner selects the first valid option");
    }

    [Test]
    public void WatchesTheAssembledBrainThroughAMonitorTheHostAttaches()
    {
        BrainMonitor monitor = new(new MonitorSettings());
        _brain.Monitor = monitor;
        _brain.Enable();

        _brain.Update();

        DecisionHistory history = monitor.ReasonerMonitors[0].History;

        Assert.Multiple(() =>
        {
            Assert.That(history.CycleCount, Is.EqualTo(1));
            Assert.That(history.GetCycle(0).SelectedName.Text, Is.EqualTo("Patrol"));
            Assert.That(history.GetRecord(0, 0).OptionName.Text, Is.EqualTo("Patrol"));
            Assert.That(history.GetRecord(0, 1).OptionName.Text, Is.EqualTo("Idle"));
        });
    }

    [Test]
    public void SwitchesWhenAGameConsiderationVetoes()
    {
        _brain.Enable();
        _brain.Update();

        _patrolGate.Allow = false;

        _brain.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Walk", "Stand" }));
    }

    [Test]
    public void ReportsThroughTheSinkTheHostWired()
    {
        _brain.Enable();
        _brain.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_logSink.Entries, Has.Some.Contains("selected Patrol"),
                "a hand-assembled brain reports where the host says");
            Assert.That(_logSink.Entries, Has.Some.Contains("Walk started"),
                "the actions the host assembled report through the same sink");
        });
    }

    [Test]
    public void SeedsAHandAssembledRollingReasoner()
    {
        Xoshiro128RandomSource seededSource = new(7);

        DualUtilityReasoner rollingReasoner = new() { RandomSource = seededSource };

        Assert.That(rollingReasoner.RandomSource, Is.SameAs(seededSource),
            "the reasoner rolls from the source the host chose, which is what makes an assembled brain reproducible");
    }

    /// <summary>A game-defined consideration, vetoing its option whenever the game turns Allow off.</summary>
    private sealed class HostGateConsideration : ConsiderationBase
    {
        /// <summary>True while the option is allowed to run.</summary>
        public bool Allow { get; set; } = true;

        /// <summary>Reports failure, since this consideration is assembled rather than loaded.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            return false;
        }

        /// <summary>Reports neutral while allowed and a veto while not.</summary>
        public override void Calculate()
        {
            Weights = Allow ? WeightValues.Neutral : WeightValues.Vetoed;
        }
    }
}
