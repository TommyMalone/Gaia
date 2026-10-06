using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Options;

/// <summary>Covers <see cref="ConsiderationAndActionOption" />, how the standard option loads its children.</summary>
[TestFixture]
public class ConsiderationAndActionOptionSpec
{
    [SetUp]
    public void BuildSink()
    {
        _logSink = new RecordingLogSink();
    }

    private RecordingLogSink _logSink;

    [Test]
    public void AssemblesInCodeWithANameAndADefaultTuning()
    {
        AiString patrolName = "Patrol";

        ConsiderationAndActionOption assembled = new(patrolName);

        // An assembled option gets its default tuning consideration from its reasoner at enable.
        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(assembled);
        reasoner.Enable();

        assembled.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(assembled.Name, Is.EqualTo(patrolName));
            Assert.That(assembled.Weight, Is.EqualTo(1f), "the default tuning is what makes a bare option selectable");
        });
    }

    [Test]
    public void LoadsItsConsiderationsAndActions()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Snipe">
                                   <Consideration Type="Tuning" Addend="2"/>
                                   <Action Type="Pause" Name="Hold" Duration="1"/>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        AiString snipeName = "Snipe";
        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Name, Is.EqualTo(snipeName));

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(2f), "the loaded tuning scores, so no default was added");
            Assert.That(loaded.IsDone, Is.True, "the pause has not started, so nothing is left to finish");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ScoresThroughTheDefaultTuningWhenNoneIsConfigured()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Idle"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(1f), "the ensured default tuning contributes an addend of 1");
            Assert.That(loaded.IsValid, Is.True);
        });
    }

    [Test]
    public void RefusesANodeWithNoName()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Option element has no Name attribute",
            }));
        });
    }

    [Test]
    public void RefusesElementsItDoesNotTake()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Snipe">
                                   <Reasoner Type="RuleBased"/>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the child before Init runs, so the test loads through the factory rather than calling
        // Init.
        bool wasCreated = creationData.Factories.Options.TryCreate(creationData, out OptionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Option element does not take Reasoner elements",
            }));
        });
    }

    [Test]
    public void WarnsWhenItsFixedValuesCanNeverSelectIt()
    {
        // A permanent veto written in tuning is sometimes a deliberate off switch, so it loads, and it is named
        // once so a designer who meant the option to run finds out at load rather than from a still character.
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Disabled">
                                   <Consideration Type="Tuning" Multiplier="0"/>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Warning  Line 1, the Option element's fixed values sum to a weight of 0 or less, a reasoner that selects by weight can never select it",
            }));
        });
    }
}
