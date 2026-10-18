using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Loads the guard from guard.xml, proving that the retention behavior <see cref="GuardSpec" /> assembles by hand
///     can ship as a configuration file.
/// </summary>
/// <remarks>
///     GuardSpec covers the Commit and Inertia patterns of <see cref="ExecutionHistoryConsideration" />, with the
///     retention factor varied per test, and the FinishedWeightFunction veto that ends the Investigate option's walk to
///     the noise once its action reports done. What is asserted here is that the file expresses the same brain at its
///     authored factor. The loaded guard investigates a noise, keeps walking on retained rank after the noise fades, and
///     always drops the walk for a threat. As a game's sensors would, the spec writes SecondsSinceNoise before each cycle
///     once a noise has been heard, and adds threats to the list it set on the blackboard at setup.
/// </remarks>
[TestFixture]
public class LoadedGuardSpec
{
    [SetUp]
    public void LoadGuard()
    {
        _timeSource = new ManualTimeSource();
        _executed = new List<string>();
        _logSink = new RecordingLogSink();
        _threats = new List<EntityBase>();
        _noiseHeardTime = null;

        _guard = new Actor { Name = "Guard" };

        FactorySet factories = new();
        factories.Actions.Register("Record", () => new RecordAction(_executed), RecordAction.Description);

        string configurationText = File.ReadAllText(ConfigurationPath);

        GaiaLoader loader = new(factories, _timeSource, new Xoshiro128RandomSource(1), _logSink);

        Brain? brain = null;
        _loaded = loader.TryParse(configurationText, "guard.xml", out SpecificationNode? root) &&
                  loader.TryLoadBrain(root!, out brain, self: _guard);

        // A failed load yields no brain. An empty stand-in keeps the fixture alive so LoadsTheWholeBrainFromTheFile can
        // report the failure instead of every test failing in SetUp.
        _brain = brain ?? new Brain();
        _guard.Brain = _brain;

        _brain.Blackboard.SetEntityList("Threats", _threats);

        _brain.Enable();
    }

    private static readonly string ConfigurationPath =
        Path.Combine(AppContext.BaseDirectory, "Integration", "guard.xml");

    private static readonly Seconds OneCycleTime = new(1f);
    private static readonly Seconds StaysRecentTime = new(3f);

    private ManualTimeSource _timeSource;
    private List<string> _executed;
    private RecordingLogSink _logSink;
    private List<EntityBase> _threats;

    /// <summary>When the noise happened, or null while none has.</summary>
    private Seconds? _noiseHeardTime;

    private Actor _guard;
    private Brain _brain;
    private bool _loaded;

    [Test]
    public void LoadsTheWholeBrainFromTheFile()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_loaded, Is.True);
            Assert.That(_brain.RootReasoner, Is.Not.Null);
            Assert.That(_logSink.Entries, Has.None.StartsWith("Error"), "a clean load reports nothing");
        });
    }

    [Test]
    public void RefusesARankSpelledWrongOnATrueElement()
    {
        // The True element on line 11 carries the Fight option's rank. Misspelled, the rank would load as nothing
        // and the guard would patrol past a known threat, so the load refuses it at that line.
        string misspelled = File.ReadAllText(ConfigurationPath).Replace("<True Rank=\"10\"/>", "<True Ranck=\"10\"/>");

        FactorySet factories = new();
        factories.Actions.Register("Record", () => new RecordAction(_executed), RecordAction.Description);

        RecordingLogSink logSink = new();
        GaiaLoader loader = new(factories, _timeSource, new Xoshiro128RandomSource(1), logSink);

        bool loaded = loader.TryLoadBrain(misspelled, "guard.xml", out Brain? brain, self: _guard);

        Assert.Multiple(() =>
        {
            Assert.That(misspelled, Does.Contain("Ranck"), "the file's line 11 is the one rewritten");
            Assert.That(loaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 11, the True element does not take the Ranck attribute",
                "Error guard.xml Line 9, the Consideration element's type EntityExists failed to initialize",
                "Error guard.xml Line 6, the Option element's type ConsiderationAndAction failed to initialize",
                "Error guard.xml Line 2, the Reasoner element's type DualUtility failed to initialize",
            }), "the refusal, then one line per enclosing component, the weight function being the element refused");
        });
    }

    [Test]
    public void KeepsWalkingAfterTheNoiseFadesOnItsAuthoredRetention()
    {
        // The Inertia pattern, straight from the file. Six tenths of rank 5 is 3, which still beats patrolling
        // at 0 once the noise has faded, so the walk continues instead of turning back.
        HearNoise();
        Cycle();

        Cycle(StaysRecentTime + OneCycleTime);

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void AlwaysDropsTheWalkForAThreat()
    {
        // Rank 10 sits above anything retention can keep.
        HearNoise();
        Cycle();

        StubEntity intruder = new("Intruder");
        _threats.Add(intruder);
        Cycle();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Investigate.Select", "Investigate.Update",
            "Investigate.Deselect", "Fight.Select", "Fight.Update",
            // @formatter:on
        }));
    }

    /// <summary>Marks the noise as heard now, so every later cycle writes how long ago it was.</summary>
    private void HearNoise()
    {
        _noiseHeardTime = _timeSource.CurrentTime;
    }

    /// <summary>Runs a cycle one second on from the previous one.</summary>
    private void Cycle()
    {
        Cycle(OneCycleTime);
    }

    /// <summary>Advances the clock, writes how long ago the noise happened as a sensor would, and runs one cycle.</summary>
    private void Cycle(Seconds delta)
    {
        _timeSource.Advance(delta);

        if (_noiseHeardTime.HasValue)
        {
            Seconds timeSince = _timeSource.CurrentTime - _noiseHeardTime.Value;
            _brain.Blackboard.SetFloat("SecondsSinceNoise", (float)timeSince.Value);
        }

        _guard.Update();
    }
}
