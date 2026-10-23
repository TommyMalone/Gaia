using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>
///     Covers <see cref="BrainMonitor" />, the walk that monitors a whole tree and the brain cycle that lines the
///     histories up.
/// </summary>
/// <remarks>
///     The fixture is a root reasoner whose one option, Snipe, holds a subreasoner action over a nested reasoner
///     whose one option is Aim. Aim is always eligible, and Snipe's one consideration is held by the fixture so a test can
///     veto it for a cycle, which is the one way to keep the nested reasoner off a cycle the root runs.
/// </remarks>
[TestFixture]
public class BrainMonitorSpec
{
    [SetUp]
    public void BuildBrain()
    {
        _executed = new List<string>();

        RecordAction aimAction = new("Aim", _executed, false);
        StubOption aim = new([new StubConsideration(WeightValues.Neutral)], [aimAction]) { Name = "Aim" };
        _nestedReasoner = new RuleBasedReasoner();
        _nestedReasoner.AddOption(aim);

        SubreasonerAction snipeStep = new(_nestedReasoner);
        _snipeGate = new StubConsideration(WeightValues.Neutral);
        StubOption snipe = new([_snipeGate], [snipeStep]) { Name = "Snipe" };
        _rootReasoner = new RuleBasedReasoner();
        _rootReasoner.AddOption(snipe);

        _brain = new Brain(_rootReasoner);
    }

    private List<string> _executed;
    private RuleBasedReasoner _nestedReasoner;
    private RuleBasedReasoner _rootReasoner;
    private StubConsideration _snipeGate;
    private Brain _brain;

    [Test]
    public void MonitorsEveryReasonerInTheTreeInTreeOrder()
    {
        BrainMonitor monitor = new(new MonitorSettings());

        _brain.Monitor = monitor;

        Assert.Multiple(() =>
        {
            Assert.That(monitor.ReasonerMonitors, Has.Count.EqualTo(2));
            Assert.That(monitor.ReasonerMonitors[0], Is.SameAs(_rootReasoner.Monitor));
            Assert.That(monitor.ReasonerMonitors[0].Path, Is.Empty, "the root reasoner sits under no option");
            Assert.That(monitor.ReasonerMonitors[1], Is.SameAs(_nestedReasoner.Monitor));
            Assert.That(monitor.ReasonerMonitors[1].Path, Is.EqualTo("Snipe"));
            Assert.That(monitor.ReasonerMonitors[1].BrainMonitor, Is.SameAs(monitor));
        });
    }

    [Test]
    public void StampsEveryHistoryWithTheBrainsCycle()
    {
        BrainMonitor monitor = new(new MonitorSettings());
        _brain.Monitor = monitor;
        _brain.Enable();

        // Snipe sits out the first cycle, so the nested reasoner runs on two of the brain's three cycles and its
        // own count cannot pass for the brain's.
        _snipeGate.Veto();
        _brain.Update();
        _snipeGate.Pass();
        _brain.Update();
        _brain.Update();

        DecisionHistory root = monitor.ReasonerMonitors[0].History;
        DecisionHistory nested = monitor.ReasonerMonitors[1].History;

        Assert.Multiple(() =>
        {
            Assert.That(monitor.CycleIndex, Is.EqualTo(3));
            Assert.That(root.GetCycleFromNewest(0).CycleIndex, Is.EqualTo(3));
            Assert.That(nested.CycleCount, Is.EqualTo(2), "the nested reasoner ran on two cycles");
            Assert.That(nested.GetCycleFromNewest(0).CycleIndex, Is.EqualTo(3),
                "the nested reasoner records the brain's cycle rather than a count of its own");
            Assert.That(nested.GetCycleFromNewest(0).SelectedName.Text, Is.EqualTo("Aim"));
        });
    }

    [Test]
    public void KeepsTheMonitorsAlreadyInPlaceWhenTheBrainIsEnabledAgain()
    {
        BrainMonitor monitor = new(new MonitorSettings());
        _brain.Monitor = monitor;
        _brain.Enable();
        _brain.Update();

        ReasonerMonitor rootMonitor = monitor.ReasonerMonitors[0];
        ReasonerMonitor nestedMonitor = monitor.ReasonerMonitors[1];

        _brain.Disable();
        _brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(monitor.ReasonerMonitors[0], Is.SameAs(rootMonitor),
                "the walk at Enable keeps what is recorded");
            Assert.That(monitor.ReasonerMonitors[1], Is.SameAs(nestedMonitor));
            Assert.That(rootMonitor.History.CycleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void MonitorsAReasonerAddedAfterItWasAttached()
    {
        BrainMonitor monitor = new(new MonitorSettings());
        _brain.Monitor = monitor;

        RuleBasedReasoner lateReasoner = new();
        StubOption fire = new([new StubConsideration(WeightValues.Neutral)], [new SubreasonerAction(lateReasoner)])
        {
            Name = "Fire",
        };
        _nestedReasoner.AddOption(fire);

        _brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(monitor.ReasonerMonitors, Has.Count.EqualTo(3), "Enable walks the tree again");
            Assert.That(monitor.ReasonerMonitors[2], Is.SameAs(lateReasoner.Monitor));
            Assert.That(monitor.ReasonerMonitors[2].Path, Is.EqualTo("Snipe/Fire"));
        });
    }
}
