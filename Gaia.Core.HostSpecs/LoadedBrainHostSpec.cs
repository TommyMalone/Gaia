using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a host can load and run the sniper using only the public surface. This project has no
///     InternalsVisibleTo grant, so anything these specs compile against is reachable from game code.
/// </summary>
/// <remarks>
///     The behavioral assertions live in LoadedSniperSpec, in Gaia.Core.Tests. What is asserted here is the host's
///     side of loading, all of it through the public surface. An action type a game registers with its own description is
///     built by name from the file through <see cref="GaiaLoader" /> and runs. A file that gives it a child, an attribute
///     or a value it does not take is refused with the line, and the description and the spelling the game registered are
///     read back through the set. Cycle counts are generous rather than exact, so this spec does not restate how many
///     cycles each step of a <see cref="SequenceReasoner" /> takes.
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

        _factories = new FactorySet();
        _factories.Actions.Register("Record", () => new HostRecordAction(_selections), HostRecordAction.Description);

        GaiaLoader loader = new(_factories, _timeSource, logSink: _logSink);

        string configurationPath = Path.Combine(AppContext.BaseDirectory, "sniper.xml");
        string configurationText = File.ReadAllText(configurationPath);

        _loaded = loader.TryLoadBrain(configurationText, "sniper.xml", out Brain? brain, self: _sniper);
        // A failed load yields no brain. An empty stand-in keeps the fixture alive so
        // LoadsTheSniperThroughThePublicSurface can report the failure instead of every test failing in SetUp.
        _brain = brain ?? new Brain();
        _sniper.Brain = _brain;
    }

    private ManualTimeSource _timeSource;
    private List<string> _selections;
    private ListLogSink _logSink;
    private FactorySet _factories;

    private Actor _sniper;
    private Brain _brain;
    private bool _loaded;

    [Test]
    public void ReadsAGameComponentsDescriptionBackThroughTheSet()
    {
        bool isRegistered = _factories.Actions.TryGetDescription("Record", out ComponentDescription? description);
        AttributeDescription done = description!.Attributes[1];

        Assert.Multiple(() =>
        {
            Assert.That(isRegistered, Is.True);
            Assert.That(description, Is.SameAs(HostRecordAction.Description));
            Assert.That(description.Abstraction, Is.EqualTo(ConceptualAbstraction.Actions));
            Assert.That(done.Spelling, Is.EqualTo("Done"));
            Assert.That(done.Kind, Is.EqualTo(AttributeKind.Bool));
            Assert.That(done.DefaultText, Is.EqualTo("true"));
            Assert.That(_factories.Actions.Types[^1], Is.EqualTo(new AiString("Record")),
                "a game's type is listed after the built-ins");
        });
    }

    [Test]
    public void ReadsAGameTypesSpellingBackThroughTheSet()
    {
        Assert.That(_factories.Actions.TypeSpellings[^1], Is.EqualTo("Record"),
            "a game's type is listed after the built-ins, spelled as the game registered it");
    }

    [Test]
    public void RefusesARecordActionWithAStrayChild()
    {
        // The action reads no children, and its description has no slots, so a consideration nested under it by
        // mistake is refused before the action's own Init runs rather than vanishing.
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Retreat">
                                                     <Action Type="Record" Name="Retreat" Done="false">
                                                         <Consideration Type="Tuning" Rank="5"/>
                                                     </Action>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;
        ListLogSink logSink = new();
        GaiaLoader loader = new(_factories, _timeSource, logSink: logSink);

        bool isLoaded = loader.TryLoadBrain(configurationText, "strays.xml", out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(isLoaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(logSink.Errors,
                Has.Member("Error strays.xml Line 5, the Action element does not take Consideration elements"),
                "the refusal is at the child's line");
        });
    }

    [Test]
    public void RefusesAnAttributeAGameComponentDoesNotDescribe()
    {
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Retreat">
                                                     <Action Type="Record" Name="Retreat" Dnoe="false"/>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;
        ListLogSink logSink = new();
        GaiaLoader loader = new(_factories, _timeSource, logSink: logSink);

        bool isLoaded = loader.TryLoadBrain(configurationText, "strays.xml", out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(isLoaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(logSink.Errors,
                Has.Member("Error strays.xml Line 4, the Action element does not take the Dnoe attribute"));
        });
    }

    [Test]
    public void ReportsAGameActionsRefusalInTheLoadersGrammar()
    {
        // A negative speed parses as a number, so the typed read accepts it. The action then refuses it through the
        // node's public reporter, so the sentence is the loader's own rather than one the action composed.
        _factories.Actions.Register("Dash", () => new HostDashAction(), HostDashAction.Description);

        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Close">
                                                     <Action Type="Dash" Name="Close" Speed="-3"/>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;
        ListLogSink logSink = new();
        GaiaLoader loader = new(_factories, _timeSource, logSink: logSink);

        bool isLoaded = loader.TryLoadBrain(configurationText, "dash.xml", out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(isLoaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(logSink.Errors[0],
                Is.EqualTo("Error dash.xml Line 4, the Action element's Speed is not 0 or above, it reads \"-3\""));
        });
    }

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
        List<EntityBase> targetsInKillZone = new();
        List<EntityBase> threatsOnRetreatRoute = new();

        // The host writes what it knows before the first cycle, not under fire and nothing in either list, and keeps
        // the two list objects, so a contact added to one below is a write the brain sees on its next cycle. With
        // nothing written the file would hide as well, since every gate carries a Missing element, but this test runs
        // the brain as a game does, with its variables written.
        _brain.Blackboard.SetBool("IsUnderFire", false);
        _brain.Blackboard.SetEntityList("TargetsInKillZone", targetsInKillZone);
        _brain.Blackboard.SetEntityList("ThreatsOnRetreatRoute", threatsOnRetreatRoute);

        _brain.Enable();

        Seconds oneCycleTime = new(1f);

        _timeSource.Advance(oneCycleTime);
        _sniper.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Hide" }), "nothing to shoot at, so the sniper hides");

        // An actor stands in for a target in the kill zone list. The file's Snipe option asks only whether that list
        // holds a valid entity, so the actor's position is not read. The library presently fills no such list, so the
        // host fills this one.
        Actor patrol = new() { Name = "Patrol", Position = new Vector3(10f, 0f, 140f) };
        targetsInKillZone.Add(patrol);

        // The shot sequence is three instantaneous steps and a two-second aim pause, and eight one-second cycles cover
        // it.
        for (int cycle = 0; cycle < 8; cycle++)
        {
            _timeSource.Advance(oneCycleTime);
            _sniper.Update();
        }

        Assert.That(_selections, Does.Contain("Fire"), "the loaded sequence fires once the aim pause elapses");
    }

    /// <summary>A game-defined action with a Speed that must be 0 or above, refused through the node's own reporter.</summary>
    private sealed class HostDashAction : ActionBase
    {
        private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Actions,
            "Moves the character at a set speed.").Add(NameAttribute);

        /// <summary>Describes the Speed attribute OnInit reads by.</summary>
        private static readonly AttributeDescription SpeedAttribute = Builder.Required("Speed", AttributeKind.Float,
            "How fast the character moves, in meters per second, 0 or above.");

        /// <summary>Describes the action for the host spec that registers it.</summary>
        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>How fast the character moves, in meters per second.</summary>
        public float Speed { get; private set; }

        /// <inheritdoc />
        public override bool IsDone => true;

        /// <summary>Reads Speed and refuses a value below 0, which parses but has no meaning a configuration could intend.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadFloat(SpeedAttribute.Name, out float speed))
            {
                return false;
            }

            if (speed < 0f)
            {
                node.TryGetString(SpeedAttribute.Name, out string speedText);
                node.ReportMalformed(SpeedAttribute.Name, speedText, "0 or above");
                return false;
            }

            Speed = speed;
            return true;
        }
    }
}
