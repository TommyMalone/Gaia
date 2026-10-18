using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Services.Time;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Actions;

/// <summary>Covers <see cref="PauseAction" />.</summary>
/// <remarks>
///     The clock is a <see cref="ManualTimeSource" />, so every duration in these tests is exact. A pause is the
///     simplest action that takes more than one cycle to finish.
/// </remarks>
[TestFixture]
public class PauseActionSpec
{
    [SetUp]
    public void BuildAction()
    {
        _timeSource = new ManualTimeSource();
        _logSink = new RecordingLogSink();
        _duration = new Seconds(10f);

        _pauseAction = new PauseAction(_duration) { TimeSource = _timeSource };
    }

    private static readonly Seconds PartOfTheDuration = new(5f);
    private static readonly Seconds PastTheDuration = new(15f);

    private ManualTimeSource _timeSource;
    private RecordingLogSink _logSink;

    private Seconds _duration;

    private PauseAction _pauseAction;

    [Test]
    public void IsDoneBeforeItIsEverSelected()
    {
        // An action that is not running has nothing left to finish.
        Assert.That(_pauseAction.IsDone, Is.True);
    }

    [Test]
    public void IsNotDoneOnTheCycleItIsSelected()
    {
        _pauseAction.Select();

        Assert.That(_pauseAction.IsDone, Is.False);
    }

    [Test]
    public void IsNotDoneBeforeItsDurationHasPassed()
    {
        _pauseAction.Select();

        Seconds almostTheDuration = new(9f);
        _timeSource.Advance(almostTheDuration);

        Assert.That(_pauseAction.IsDone, Is.False);
    }

    [Test]
    public void IsDoneWhenItsDurationHasPassedExactly()
    {
        // A duration that lands on a cycle boundary is common, so the comparison has to include the boundary.
        _pauseAction.Select();

        _timeSource.Advance(_duration);

        Assert.That(_pauseAction.IsDone, Is.True);
    }

    [Test]
    public void StaysDoneOnceItsDurationHasPassed()
    {
        _pauseAction.Select();

        _timeSource.Advance(PastTheDuration);

        Assert.That(_pauseAction.IsDone, Is.True);

        _timeSource.Advance(PastTheDuration);

        Assert.That(_pauseAction.IsDone, Is.True);
    }

    [Test]
    public void IgnoresTimeThatPassedBeforeItWasSelected()
    {
        // The wait starts when the action is selected rather than when it was built.
        Seconds idleTime = new(100f);
        _timeSource.Advance(idleTime);

        _pauseAction.Select();

        _timeSource.Advance(PartOfTheDuration);

        Assert.That(_pauseAction.IsDone, Is.False);
    }

    [Test]
    public void StartsTheWaitOverWhenSelectedAgain()
    {
        _pauseAction.Select();

        _timeSource.Advance(PastTheDuration);

        _pauseAction.Deselect();
        _pauseAction.Select();

        _timeSource.Advance(PartOfTheDuration);

        Assert.That(_pauseAction.IsDone, Is.False);
    }

    [Test]
    public void IsDoneOnceDeselectedPartWayThroughItsWait()
    {
        // A deselected pause is not running, and an action that is not running has nothing left to finish, however
        // much of its wait was left.
        _pauseAction.Select();

        _timeSource.Advance(PartOfTheDuration);

        _pauseAction.Deselect();

        Assert.That(_pauseAction.IsDone, Is.True);
    }

    [Test]
    public void IsDoneImmediatelyWithNoDuration()
    {
        PauseAction pauseAction = new(Seconds.Zero) { TimeSource = _timeSource };

        pauseAction.Select();

        Assert.That(pauseAction.IsDone, Is.True);
    }

    [Test]
    public void LoadsItsNameAndDuration()
    {
        const string snippet = """
                               <Action Type="Pause" Name="Wait" Duration="2"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        creationData.TimeSource = _timeSource;

        AiString waitName = "Wait";
        Seconds waitTime = new(2f);
        PauseAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Name, Is.EqualTo(waitName));
            Assert.That(loaded.Duration, Is.EqualTo(waitTime));

            loaded.Select();

            Assert.That(loaded.IsDone, Is.False, "the loaded wait has just started");

            _timeSource.Advance(waitTime);

            Assert.That(loaded.IsDone, Is.True, "the loaded clock came from the creation data");
        });
    }

    [Test]
    public void RefusesANodeWithNoDuration()
    {
        const string snippet = """
                               <Action Type="Pause" Name="Wait"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        PauseAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element has no Duration attribute",
            }));
        });
    }
}
