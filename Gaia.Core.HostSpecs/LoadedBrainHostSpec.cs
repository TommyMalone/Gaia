using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a host can load and run the sniper using only the public surface. This project has no
///     InternalsVisibleTo grant, so anything these specs compile against is reachable from game code.
/// </summary>
/// <remarks>
///     The behavioral assertions live in LoadedSniperSpec, in Gaia.Core.Tests. What is asserted here is the host's
///     contract, the configuration loads through <see cref="GaiaLoader" />, a game-registered action type is built by name
///     from the file, and the loaded brain runs. Cycle counts are generous rather than exact, so this spec does not
///     restate how many cycles each step of a <see cref="SequenceReasoner" /> takes.
/// </remarks>
[TestFixture]
public class LoadedBrainHostSpec
{
    [SetUp]
    public void LoadSniper()
    {
        _timeSource = new ManualTimeSource();
        _selections = new List<string>();
        _logSink = new ListLogSink();

        _sniper = new Actor
        {
            Name = "Sniper",
            Position = new Vector3(0f, 10f, 0f),
        };

        FactorySet factories = new();
        factories.Actions.Register("Record", () => new HostRecordAction(_selections));

        GaiaLoader loader = new(factories, _timeSource, logSink: _logSink);

        string configurationPath = Path.Combine(AppContext.BaseDirectory, "sniper.xml");
        string configurationText = File.ReadAllText(configurationPath);

        _loaded = loader.TryLoadBrain(configurationText, "sniper.xml", out Brain? brain, self: _sniper);
        _brain = brain ?? new Brain();
        _sniper.Brain = _brain;
    }

    private ManualTimeSource _timeSource;
    private List<string> _selections;
    private ListLogSink _logSink;

    private Actor _sniper;
    private Brain _brain;
    private bool _loaded;

    [Test]
    public void LoadsTheSniperThroughThePublicSurface()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_loaded, Is.True);
            Assert.That(_brain.RootReasoner, Is.Not.Null);
            Assert.That(_logSink.Errors, Is.Empty, "a clean load reports nothing");
        });
    }

    [Test]
    public void RunsTheLoadedBrain()
    {
        List<EntityBase> killZoneContacts = new();
        List<EntityBase> retreatRouteContacts = new();

        _brain.Blackboard.SetBool("IsUnderFire", false);
        _brain.Blackboard.SetEntityList("KillZoneContacts", killZoneContacts);
        _brain.Blackboard.SetEntityList("RetreatRouteContacts", retreatRouteContacts);

        _brain.Enable();

        Seconds oneCycleTime = new(1f);

        _timeSource.Advance(oneCycleTime);
        _sniper.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Hide" }), "nothing to shoot at, so the sniper hides");

        // An actor stands in for a contact in the kill zone list. The file's Snipe option asks only whether that list
        // holds a valid entity, so the actor's position is not read. The library presently fills no contact list, so
        // the host fills this one.
        Actor patrol = new() { Name = "Patrol", Position = new Vector3(10f, 0f, 140f) };
        killZoneContacts.Add(patrol);

        // The shot sequence is three instantaneous steps and a two-second aim pause, and eight one-second cycles cover
        // it.
        for (int cycle = 0; cycle < 8; cycle++)
        {
            _timeSource.Advance(oneCycleTime);
            _sniper.Update();
        }

        Assert.That(_selections, Does.Contain("Fire"), "the loaded sequence fires once the aim pause elapses");
    }
}
