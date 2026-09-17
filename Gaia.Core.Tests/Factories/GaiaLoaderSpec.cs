using Gaia.Core.Blackboards;
using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="GaiaLoader" />, the brains it builds and the loads it refuses.</summary>
/// <remarks>
///     A whole brain loading and running lives in <see cref="Integration.LoadedSniperSpec" />, which loads the sniper
///     through this surface. What is covered here is the loader's own contract, a refused load reported under the source
///     name and the seed reported when the host supplies no random source.
/// </remarks>
[TestFixture]
public class GaiaLoaderSpec
{
    [SetUp]
    public void BuildLoader()
    {
        _logSink = new RecordingLogSink();
        _timeSource = new ManualTimeSource();

        // A master is supplied so the loader reports no rolled seed into the sink the tests read.
        FixedRandomSource master = new(0f);
        _loader = new GaiaLoader(new FactorySet(), _timeSource, master, _logSink);
    }

    private const string SourceName = "guard.xml";

    private RecordingLogSink _logSink;
    private ManualTimeSource _timeSource;
    private GaiaLoader _loader;

    [Test]
    public void PointsTheStringTableAtItsSink()
    {
        // The table is process wide, so the standing sink is put back afterward. The colliding pair is the one
        // AiStringTableSpec derives, and it stays registered for the rest of the run.
        ILogSink standingSink = AiStringTable.LogSink;

        try
        {
            RecordingLogSink hostSink = new();
            _ = new GaiaLoader(new FactorySet(), _timeSource, new FixedRandomSource(0f), hostSink);

            AiString first = "ba";
            AiString collider = "a\u0082";

            Assert.Multiple(() =>
            {
                Assert.That(first == collider, Is.True, "the two texts share a hash");
                Assert.That(hostSink.Entries, Has.Some.Contains("hash to the same value"),
                    "constructing the loader pointed the table's report at the host's sink");
            });
        }
        finally
        {
            AiStringTable.LogSink = standingSink;
        }
    }

    [Test]
    public void LoadsABrainFromConfigurationText()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;

        bool loaded = _loader.TryLoadBrain(snippet, SourceName, out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.True);
            Assert.That(brain!.RootReasoner, Is.InstanceOf<RuleBasedReasoner>());
            Assert.That(_logSink.Entries, Is.Empty, "a clean load reports nothing");
        });
    }

    [Test]
    public void WatchesEveryBrainItLoadsUnderItsDiagnostics()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        MonitorSettings settings = new(32, 4);
        _loader.Diagnostics = settings;

        _loader.TryLoadBrain(snippet, SourceName, out Brain? first);
        _loader.TryLoadBrain(snippet, SourceName, out Brain? second);

        Assert.Multiple(() =>
        {
            Assert.That(first!.Monitor, Is.Not.Null);
            Assert.That(first.Monitor!.Settings, Is.SameAs(settings));
            Assert.That(first.Monitor.ReasonerMonitors, Has.Count.EqualTo(1), "the loaded tree is walked at attach");
            Assert.That(second!.Monitor, Is.Not.SameAs(first.Monitor), "each brain gets a monitor of its own");
        });
    }

    [Test]
    public void LetsOneLoadCarryItsOwnDiagnostics()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        MonitorSettings closeWatch = new(1024, 16);

        _loader.TryLoadBrain(snippet, SourceName, out Brain? fleetBrain);
        _loader.TryLoadBrain(snippet, SourceName, out Brain? watchedBrain, diagnostics: closeWatch);

        Assert.Multiple(() =>
        {
            Assert.That(fleetBrain!.Monitor, Is.Null, "a loader carrying no settings attaches nothing");
            Assert.That(watchedBrain!.Monitor!.Settings, Is.SameAs(closeWatch));
        });
    }

    [Test]
    public void StampsItsFaultPolicyOnEveryBrainItLoads()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;

        _loader.TryLoadBrain(snippet, SourceName, out Brain? rethrowing);

        _loader.FaultPolicy = FaultPolicy.Contain;
        _loader.TryLoadBrain(snippet, SourceName, out Brain? containing);

        Assert.Multiple(() =>
        {
            Assert.That(rethrowing!.FaultPolicy, Is.EqualTo(FaultPolicy.Rethrow), "the default lets a fault out");
            Assert.That(containing!.FaultPolicy, Is.EqualTo(FaultPolicy.Contain));
        });
    }

    [Test]
    public void YieldsNoBrainFromAMalformedDocument()
    {
        bool loaded = _loader.TryLoadBrain("<Brain>", SourceName, out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(_logSink.Entries, Has.Count.EqualTo(1));
            Assert.That(_logSink.Entries[0],
                Does.StartWith("Error guard.xml Line 1, the document could not be parsed."));
        });
    }

    [Test]
    public void YieldsNoBrainWhenAComponentRefusesItsConfiguration()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="Nope"/>
                               </Brain>
                               """;

        bool loaded = _loader.TryLoadBrain(snippet, SourceName, out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.False);
            Assert.That(brain, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Reasoner element's type Nope is not registered",
            }));
        });
    }

    [Test]
    public void LoadsManyBrainsFromOneParsedTree()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;

        bool parsed = _loader.TryParse(snippet, SourceName, out SpecificationNode? root);

        bool firstLoaded = _loader.TryLoadBrain(root!, out Brain? first);
        bool secondLoaded = _loader.TryLoadBrain(root!, out Brain? second);

        Assert.Multiple(() =>
        {
            Assert.That(parsed && firstLoaded && secondLoaded, Is.True);
            Assert.That(first!.RootReasoner, Is.Not.SameAs(second!.RootReasoner),
                "each load builds its own components");
            Assert.That(first.Blackboard, Is.Not.SameAs(second.Blackboard), "each load makes its own blackboard");
        });
    }

    [Test]
    public void GivesTheLoadedBrainABlackboardTheHostKept()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        BrainBlackboard kept = new();
        kept.SetBool("SawPlayer", true);

        bool loaded = _loader.TryLoadBrain(snippet, SourceName, out Brain? brain, brainBlackboard: kept);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.True);
            Assert.That(brain!.Blackboard, Is.SameAs(kept), "the brain keeps the given blackboard over making its own");
            Assert.That(brain.Blackboard.TryGetBool("SawPlayer", out bool sawPlayer) && sawPlayer, Is.True,
                "what was written before the load is still there after it");
        });
    }

    [Test]
    public void GivesEachBrainAStreamOfItsOwn()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="DualUtility"/>
                               </Brain>
                               """;
        Xoshiro128RandomSource masterStream = new(7);
        GaiaLoader seededLoader = new(new FactorySet(), _timeSource, masterStream, _logSink);

        bool parsed = seededLoader.TryParse(snippet, SourceName, out SpecificationNode? root);

        bool firstLoaded = seededLoader.TryLoadBrain(root!, out Brain? first);
        bool secondLoaded = seededLoader.TryLoadBrain(root!, out Brain? second);

        DualUtilityReasoner firstReasoner = (DualUtilityReasoner)first!.RootReasoner!;
        DualUtilityReasoner secondReasoner = (DualUtilityReasoner)second!.RootReasoner!;

        Assert.Multiple(() =>
        {
            Assert.That(parsed && firstLoaded && secondLoaded, Is.True);
            Assert.That(firstReasoner.RandomSource, Is.Not.SameAs(masterStream),
                "the master seeds streams rather than being handed out");
            Assert.That(firstReasoner.RandomSource, Is.Not.SameAs(secondReasoner.RandomSource),
                "one character's draws never shift another's");
            Assert.That(firstReasoner.RandomSource.NextFloat(), Is.Not.EqualTo(secondReasoner.RandomSource.NextFloat()),
                "the two sources were seeded from different draws of the master, so their streams differ");
        });
    }

    [Test]
    public void ScriptsABrainThroughASourceHandedToItsLoad()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="DualUtility"/>
                               </Brain>
                               """;
        FixedRandomSource scriptedRolls = new(0.5f);

        bool parsed = _loader.TryParse(snippet, SourceName, out SpecificationNode? root);
        bool loaded = _loader.TryLoadBrain(root!, out Brain? brain, randomSource: scriptedRolls);

        DualUtilityReasoner reasoner = (DualUtilityReasoner)brain!.RootReasoner!;

        Assert.Multiple(() =>
        {
            Assert.That(parsed && loaded, Is.True);
            Assert.That(reasoner.RandomSource, Is.SameAs(scriptedRolls),
                "a source handed to the load is the brain's own");
        });
    }

    [Test]
    public void GivesANamedBrainTheSameStreamInAnyLoadOrder()
    {
        // Two loaders on one master seed load the same pair of characters, Aster first for one and last for the
        // other, so where Aster sits in line is the only thing that differs between its two brains. One parsed tree
        // serves both loaders, since loading reads a tree and only the reader ever writes to one.
        const int masterSeed = 7;
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="DualUtility"/>
                               </Brain>
                               """;
        Actor aster = new() { Name = "Aster" };
        Actor briar = new() { Name = "Briar" };

        GaiaLoader loaderWithAsterFirst =
            new(new FactorySet(), _timeSource, new Xoshiro128RandomSource(masterSeed), _logSink);
        GaiaLoader loaderWithAsterLast =
            new(new FactorySet(), _timeSource, new Xoshiro128RandomSource(masterSeed), _logSink);

        loaderWithAsterFirst.TryParse(snippet, SourceName, out SpecificationNode? root);

        loaderWithAsterFirst.TryLoadBrain(root!, out Brain? asterLoadedFirst, self: aster);
        loaderWithAsterFirst.TryLoadBrain(root!, out Brain? _, self: briar);

        loaderWithAsterLast.TryLoadBrain(root!, out Brain? _, self: briar);
        loaderWithAsterLast.TryLoadBrain(root!, out Brain? asterLoadedLast, self: aster);

        Assert.That(SeedOf(asterLoadedFirst!), Is.EqualTo(SeedOf(asterLoadedLast!)),
            "a named brain's stream comes from the master seed and its name, not its place in line");
    }

    [Test]
    public void GivesUnnamedActorsBrainsStreamsOfTheirOwn()
    {
        // An actor that was never named carries the empty name, and deriving from it would hand every such brain
        // one stream, so an unnamed brain costs the master a draw instead, the way a brain with no actor does.
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="DualUtility"/>
                               </Brain>
                               """;
        GaiaLoader loader = new(new FactorySet(), _timeSource, new Xoshiro128RandomSource(7), _logSink);

        loader.TryParse(snippet, SourceName, out SpecificationNode? root);
        loader.TryLoadBrain(root!, out Brain? firstBrain, self: new Actor());
        loader.TryLoadBrain(root!, out Brain? secondBrain, self: new Actor());

        Assert.That(SeedOf(firstBrain!), Is.Not.EqualTo(SeedOf(secondBrain!)),
            "two unnamed brains get streams of their own rather than the one the empty name would derive");
    }

    [Test]
    public void ReportsTheSeedItRolledWhenGivenNoRandomSource()
    {
        RecordingLogSink seedSink = new();

        _ = new GaiaLoader(new FactorySet(), _timeSource, null, seedSink);

        Assert.Multiple(() =>
        {
            Assert.That(seedSink.Entries, Has.Count.EqualTo(1));
            Assert.That(seedSink.Entries[0], Does.StartWith("Message Gaia random seed "),
                "the reported seed is what reproduces the run");
        });
    }

    /// <summary>The seed of the stream a loaded brain was given.</summary>
    /// <remarks>A brain holds no source of its own, so what a spec can read is the one its load handed down to the reasoner.</remarks>
    private static int SeedOf(Brain brain)
    {
        return ((Xoshiro128RandomSource)((DualUtilityReasoner)brain.RootReasoner!).RandomSource).Seed;
    }
}
