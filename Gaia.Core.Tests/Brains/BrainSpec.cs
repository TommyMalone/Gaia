using Gaia.Core.Brains;
using Gaia.Core.Reasoners;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Brains;

/// <summary>Covers how enabling, disabling and updating a <see cref="Brain" /> reaches whatever its reasoner runs.</summary>
/// <remarks>
///     The reasoner is a <see cref="RuleBasedReasoner" /> rather than a stub, with one always-valid option holding a
///     <see cref="RecordAction" />, so what is asserted is which lifecycle calls reached the action rather than anything
///     about how the reasoner decided.
/// </remarks>
[TestFixture]
public class BrainSpec
{
    [SetUp]
    public void BuildBrain()
    {
        _logSink = new RecordingLogSink();
        _executed = new List<string>();

        RecordAction action = new("Idle", _executed, false);
        StubOption option = new([], [action]);

        _reasoner = new RuleBasedReasoner();
        _reasoner.AddOption(option);

        _brain = new Brain { RootReasoner = _reasoner };
    }

    private RecordingLogSink _logSink;

    private List<string> _executed;

    private RuleBasedReasoner _reasoner;
    private Brain _brain;

    [Test]
    public void StartsDisabled()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.False);
            Assert.That(_reasoner.IsEnabled, Is.False);
        });
    }

    [Test]
    public void HasABlackboardOfItsOwn()
    {
        Brain anotherBrain = new();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.Blackboard, Is.Not.Null);
            Assert.That(_brain.Blackboard, Is.Not.SameAs(anotherBrain.Blackboard));
        });
    }

    [Test]
    public void EnablesItsReasonerWhenEnabled()
    {
        _brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.True);
            Assert.That(_reasoner.IsEnabled, Is.True);
        });
    }

    [Test]
    public void UpdatesItsReasonerWhileEnabled()
    {
        _brain.Enable();

        _brain.Update();
        _brain.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update", "Idle.Update" }));
    }

    [Test]
    public void DoesNothingWhenUpdatedBeforeBeingEnabled()
    {
        _brain.Update();

        Assert.That(_executed, Is.Empty);
    }

    [Test]
    public void TearsDownWhatItWasRunningWhenDisabled()
    {
        // Disabling the reasoner deselects its option, which deselects the action. The player taking control of the
        // AI character should not leave the AI's last action running underneath.
        _brain.Enable();
        _brain.Update();

        _brain.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.False);
            Assert.That(_reasoner.IsEnabled, Is.False);
            Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update", "Idle.Deselect" }));
        });
    }

    [Test]
    public void StaysQuietAfterBeingDisabled()
    {
        _brain.Enable();
        _brain.Update();
        _brain.Disable();

        _executed.Clear();

        _brain.Update();

        Assert.That(_executed, Is.Empty);
    }

    [Test]
    public void StartsAgainWhenReEnabled()
    {
        _brain.Enable();
        _brain.Update();
        _brain.Disable();

        _executed.Clear();

        _brain.Enable();
        _brain.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update" }));
    }

    [Test]
    public void EnablesOnlyOnce()
    {
        // A second Enable must not restart the reasoner. A SequenceReasoner makes that observable, since enabling one
        // puts it back at its first option. The sequence here is mid-run, so a restart would run First again instead
        // of finishing.
        RecordAction firstAction = new("First", _executed);
        RecordAction secondAction = new("Second", _executed, false);

        StubOption firstStep = new([], [firstAction]);
        StubOption secondStep = new([], [secondAction]);

        SequenceReasoner sequence = new();
        sequence.AddOption(firstStep);
        sequence.AddOption(secondStep);

        Brain brain = new() { RootReasoner = sequence };

        brain.Enable();
        brain.Update();
        brain.Update();

        brain.Enable();

        secondAction.SetDone(true);
        brain.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Deselect", "Second.Select", "Second.Update",
            "Second.Deselect",
            // @formatter:on
        }));
    }

    [Test]
    public void DoesNothingWithNoReasoner()
    {
        Brain emptyBrain = new();

        emptyBrain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(() => emptyBrain.Update(), Throws.Nothing);
            Assert.That(() => emptyBrain.Disable(), Throws.Nothing);
        });
    }

    [Test]
    public void WarnsWhenItIsEnabledWithNoReasoner()
    {
        Brain emptyBrain = new()
        {
            LogSink = _logSink,
            LogChannel = "Sentry",
        };

        emptyBrain.Enable();

        Assert.That(_logSink.Entries, Is.EqualTo(new[] { "Warning Sentry brain enabled with no root reasoner" }));
    }
}
