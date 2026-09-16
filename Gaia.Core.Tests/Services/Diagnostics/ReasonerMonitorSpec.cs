using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>
///     Covers <see cref="ReasonerMonitor" /> attached by hand to a bare reasoner, the cycles it counts and the two
///     shapes it warns about. What a recorded cycle holds is covered where the roll is made, in
///     <see cref="Reasoners.DualUtilityReasonerSpec" />, and the brain's cycle in <see cref="BrainMonitorSpec" />.
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

    [Test]
    public void WarnsWhenItsSelectionHasBeenDoneForAWholeWindow()
    {
        // The option's one action is done at Select and nothing vetoes the option afterward, which is the
        // missing-finished-veto shape the warning exists to name.
        RecordingLogSink logSink = new();
        RuleBasedReasoner reasoner = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings()),
        };

        StubOption holdingOption = new([new StubConsideration(WeightValues.Neutral)], [new RecordAction(_executed)])
            { Name = "Hold" };

        reasoner.AddOption(holdingOption);
        reasoner.Enable();

        for (int cycle = 0; cycle < 60; cycle++)
        {
            reasoner.Update();
        }

        Assert.That(logSink.Entries, Has.One.Contains("the Hold option has been done for 60 cycles"),
            "the warning fires once when the count reaches the window");
    }

    [Test]
    public void WarnsWhenNothingHasBeenSelectableForAWholeWindow()
    {
        // Every option vetoed is the standing-still shape, a character doing nothing with nothing reported.
        RecordingLogSink logSink = new();
        RuleBasedReasoner reasoner = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings()),
        };

        StubOption blockedOption = new([new StubConsideration(WeightValues.Vetoed)], []) { Name = "Blocked" };

        reasoner.AddOption(blockedOption);
        reasoner.Enable();

        for (int cycle = 0; cycle < 60; cycle++)
        {
            reasoner.Update();
        }

        Assert.That(logSink.Entries, Has.One.Contains("nothing has been selectable for 60 cycles"),
            "the warning fires once when the count reaches the window");
    }

    [Test]
    public void StaysQuietAboutSelectingNothingOnceASequenceHasRunOffItsEnd()
    {
        // A sequence past its last option selects nothing until its parent ends it. The nothing-selected warning
        // is for a reasoner with work left, so the IsDone guard keeps it quiet here.
        RecordingLogSink logSink = new();
        SequenceReasoner sequence = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings(nothingSelectedWarningWindow: 2)),
        };

        sequence.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [new RecordAction(_executed)])
            { Name = "Step" });
        sequence.Enable();

        for (int cycle = 0; cycle < 5; cycle++)
        {
            sequence.Update();
        }

        Assert.Multiple(() =>
        {
            Assert.That(sequence.IsDone, Is.True, "the sequence ran off its end");
            Assert.That(logSink.Entries, Has.None.Contains("nothing has been selectable"),
                "a sequence that ran out of work is finished, not standing still");
        });
    }
}
