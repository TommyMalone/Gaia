using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Options;

/// <summary>Covers <see cref="ConsiderationAndActionOption" />, how the standard option loads its children.</summary>
[TestFixture]
public class ConsiderationAndActionOptionSpec
{
    [Test]
    public void LoadsItsConsiderationsAndActions()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Snipe">
                                   <Consideration Type="Tuning" Addend="2"/>
                                   <Action Type="Pause" Name="Hold" Duration="1"/>
                               </Option>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        AiString snipeName = "Snipe";
        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Name, Is.EqualTo(snipeName));

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(2f), "the loaded tuning scores, so no default was added");
            Assert.That(loaded.IsDone, Is.True, "the pause has not started, so nothing is left to finish");
            Assert.That(logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ScoresThroughTheDefaultTuningWhenNoneIsConfigured()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Idle"/>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

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
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(logSink.Entries, Is.EqualTo(new[]
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
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Option element does not take Reasoner elements",
            }));
        });
    }
}
