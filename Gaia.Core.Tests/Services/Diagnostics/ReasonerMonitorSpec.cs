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
            Assert.That(_monitor.History.GetCycleFromNewest(0).CycleIndex, Is.EqualTo(3),
                "the newest cycle is the third seen");
            Assert.That(_monitor.History.GetCycleFromNewest(2).CycleIndex, Is.EqualTo(1), "the oldest is the first");
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
        // The option's one action is done at Select and nothing vetoes the option afterward, which is the shape
        // MonitorSettings.DoneWarningWindow describes, a finished veto missing.
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
    public void StartsTheDoneCountOverForEachOptionSelected()
    {
        // Both options are done at Select. Hold is selected for two cycles, then vetoed, so Wait is selected straight
        // after it, done from its first cycle. Four done cycles in a row pass the window of 3, but Wait has been done
        // for only two of them, so nothing is warned of until its third.
        RecordingLogSink logSink = new();
        RuleBasedReasoner reasoner = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings(doneWarningWindow: 3)),
        };

        StubConsideration holdingGate = new(WeightValues.Neutral);
        StubOption holdingOption = new([holdingGate], [new RecordAction(_executed)]) { Name = "Hold" };
        StubOption waitingOption = new([new StubConsideration(WeightValues.Neutral)], [new RecordAction(_executed)])
            { Name = "Wait" };

        reasoner.AddOption(holdingOption);
        reasoner.AddOption(waitingOption);
        reasoner.Enable();

        reasoner.Update();
        reasoner.Update();
        holdingGate.Veto();
        reasoner.Update();
        reasoner.Update();

        Assert.That(logSink.Entries, Has.None.Contains("has been done for"), "Wait has been done for two cycles");

        reasoner.Update();

        Assert.That(logSink.Entries, Has.One.Contains("the Wait option has been done for 3 cycles"));
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

    [TestCase(1)]
    [TestCase(60)]
    public void StaysQuietWhileItsSelectionIsRunning(int window)
    {
        // The option's one action never finishes, so the reasoner has a selection that is not done on every cycle, the
        // shape neither warning is for. A window of 1 is reached on the first cycle, so a count wrongly kept there
        // warns at once, and a window of 60 catches a count wrongly kept across cycles.
        RecordingLogSink logSink = new();
        RuleBasedReasoner reasoner = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings(doneWarningWindow: window,
                nothingEligibleForSelectionWarningWindow: window)),
        };

        StubOption runningOption = new([new StubConsideration(WeightValues.Neutral)],
            [new RecordAction("Run", _executed, false)]) { Name = "Run" };

        reasoner.AddOption(runningOption);
        reasoner.Enable();

        for (int cycle = 0; cycle < window; cycle++)
        {
            reasoner.Update();
        }

        Assert.Multiple(() =>
        {
            Assert.That(reasoner.SelectedOption, Is.SameAs(runningOption));
            Assert.That(logSink.Entries, Has.None.StartsWith("Warning"));
        });
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
            Monitor = new ReasonerMonitor(new MonitorSettings(nothingEligibleForSelectionWarningWindow: 2)),
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

    [Test]
    public void StaysQuietWhenItsOptionsExactlyFillTheRecord()
    {
        // A record of two options holds both of a reasoner's two options, so none goes unrecorded.
        RecordingLogSink logSink = new();
        RuleBasedReasoner reasoner = new()
        {
            LogSink = logSink,
            Monitor = new ReasonerMonitor(new MonitorSettings(optionCapacity: 2)),
        };

        reasoner.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [new RecordAction(_executed)])
            { Name = "Walk" });
        reasoner.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [new RecordAction(_executed)])
            { Name = "Wait" });
        reasoner.Enable();

        reasoner.Update();

        Assert.That(logSink.Entries, Has.None.Contains("go unrecorded"));
    }

    [Test]
    public void NamesTheNestedReasonerAWarningCameFrom()
    {
        // A brain monitor gives a nested reasoner's monitor the path of options above it, and a root reasoner's an
        // empty one. Each reasoner here has every option vetoed and a window of 1, so each warns on its first cycle.
        RecordingLogSink logSink = new();
        RuleBasedReasoner rootReasoner = new()
        {
            LogSink = logSink,
            LogChannel = "Sniper",
            Monitor = new ReasonerMonitor(new MonitorSettings(nothingEligibleForSelectionWarningWindow: 1)),
        };
        RuleBasedReasoner nestedReasoner = new()
        {
            LogSink = logSink,
            LogChannel = "Sniper",
            Monitor = new ReasonerMonitor(new MonitorSettings(nothingEligibleForSelectionWarningWindow: 1))
                { Path = "Snipe/Fire" },
        };

        rootReasoner.AddOption(new StubOption([new StubConsideration(WeightValues.Vetoed)], []) { Name = "Blocked" });
        nestedReasoner.AddOption(new StubOption([new StubConsideration(WeightValues.Vetoed)], []) { Name = "Blocked" });
        rootReasoner.Enable();
        nestedReasoner.Enable();

        rootReasoner.Update();
        nestedReasoner.Update();

        Assert.That(logSink.Entries, Is.EqualTo(new[]
        {
            "Warning Sniper nothing has been selectable for 1 cycles, every option is vetoed or scores nothing",
            "Warning Sniper Snipe/Fire, nothing has been selectable for 1 cycles, every option is vetoed or scores nothing",
        }));
    }
}
