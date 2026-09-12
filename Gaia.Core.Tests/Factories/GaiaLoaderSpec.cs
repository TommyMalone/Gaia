using Gaia.Core.Blackboards;
using Gaia.Core.Brains;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
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
        SystemRandomSource masterStream = new(7);
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
}
