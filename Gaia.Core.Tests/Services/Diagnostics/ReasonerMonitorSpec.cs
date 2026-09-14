using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>
///     Covers <see cref="ReasonerMonitor" /> attached by hand to a bare reasoner. What a recorded cycle holds is
///     covered where the roll is made, in <see cref="Reasoners.DualUtilityReasonerSpec" />, and the brain's cycle in
///     <see cref="BrainMonitorSpec" />.
/// </summary>
[TestFixture]
public class ReasonerMonitorSpec
{
    [SetUp]
    public void BuildReasoner()
    {
        _executed = new List<string>();
        _monitor = new ReasonerMonitor(new MonitorSettings());

        RecordAction walkAction = new("Walk", _executed, false);
        StubOption walk = new([new StubConsideration(WeightValues.Neutral)], [walkAction]) { Name = "Walk" };

        _reasoner = new RuleBasedReasoner { Monitor = _monitor };
        _reasoner.AddOption(walk);
        _reasoner.Enable();
    }

    private List<string> _executed;
    private ReasonerMonitor _monitor;
    private RuleBasedReasoner _reasoner;

    [Test]
    public void CountsTheCyclesItHasSeenWhenNoBrainMonitorMadeIt()
    {
        _reasoner.Update();
        _reasoner.Update();
        _reasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_monitor.BrainMonitor, Is.Null);
            Assert.That(_monitor.Path, Is.Empty);
            Assert.That(_monitor.History.CycleCount, Is.EqualTo(3));
            Assert.That(_monitor.History.GetCycle(0).CycleIndex, Is.EqualTo(3), "the newest cycle is the third seen");
            Assert.That(_monitor.History.GetCycle(2).CycleIndex, Is.EqualTo(1), "the oldest is the first");
        });
    }

    [Test]
    public void RecordsNothingWhileItsReasonerIsDisabled()
    {
        _reasoner.Disable();

        _reasoner.Update();

        Assert.That(_monitor.History.CycleCount, Is.Zero, "a disabled reasoner runs no cycle to record");
    }
}
