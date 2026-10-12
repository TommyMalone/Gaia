using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

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
    public void CountsATuningInAGroupBuiltInCode()
    {
        // The group's constructor fills it before AddConsideration hands it to the option, which is when the option's
        // set records whether the group holds a Tuning.
        AiString patrolName = "Patrol";
        WeightValues tunedWeights = new(5f, 1f, float.MinValue);
        TuningConsideration tuning = new(tunedWeights);
        GroupConsideration group = new([tuning]);

        ConsiderationAndActionOption assembled = new(patrolName);
        assembled.AddConsideration(group);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(assembled);
        reasoner.Enable();

        assembled.Calculate();

        Assert.That(assembled.Weight, Is.EqualTo(5f), "a default tuning beside the group would make it 6");
    }

    [Test]
    public void CountsATuningInsideAGroupAGameDerives()
    {
        // The group derives the group base and answers with Totals, so it reports the Tuning's values unchanged, and
        // the option's weight tells whether a default Tuning was added beside it.
        AiString patrolName = "Patrol";
        WeightValues tunedWeights = new(5f, 1f, float.MinValue);
        TuningConsideration tuning = new(tunedWeights);
        RecordingGroupConsideration group = new([tuning]);

        ConsiderationAndActionOption assembled = new(patrolName);
        assembled.AddConsideration(group);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(assembled);
        reasoner.Enable();

        assembled.Calculate();

        Assert.That(assembled.Weight, Is.EqualTo(5f), "a default tuning beside the game's group would make it 6");
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
    public void CountsATuningInsideAGroupAsItsOwn()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Patrol">
                                   <Consideration Type="Group">
                                       <Consideration Type="Tuning" Addend="5"/>
                                   </Consideration>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(5f), "a default tuning beside the group would make it 6");
            Assert.That(loaded.Considerations, Has.Count.EqualTo(1), "the group alone, with no default beside it");
        });
    }

    [Test]
    public void CountsATuningTwoGroupsDeep()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Patrol">
                                   <Consideration Type="Group">
                                       <Consideration Type="Group">
                                           <Consideration Type="Tuning" Addend="5"/>
                                       </Consideration>
                                   </Consideration>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(5f), "a default tuning beside the outer group would make it 6");
        });
    }

    [Test]
    public void AddsTheDefaultBesideAnEmptyGroup()
    {
        // An empty group reports an addend of 0 and a multiplier of 1, so the option weighs 1 with the default and 0
        // without it.
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Idle">
                                   <Consideration Type="Group"/>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        loaded.Calculate();

        GroupConsideration group = (GroupConsideration)loaded.Considerations[0];

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Weight, Is.EqualTo(1f), "the default's addend of 1 is the whole weight");
            Assert.That(loaded.Considerations[^1], Is.InstanceOf<TuningConsideration>(), "the default after the group");
            Assert.That(loaded.Considerations, Has.Count.EqualTo(2));
            Assert.That(group.Considerations, Is.Empty, "the group gains no default of its own");
        });
    }

    [Test]
    public void AddsTheDefaultBesideAGroupThatHoldsNoTuning()
    {
        // The group holds a consideration that is not a Tuning, so the test fails if any group that holds something
        // counts as holding a Tuning.
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Watch">
                                   <Consideration Type="Group">
                                       <Consideration Type="ExecutionHistory"/>
                                   </Consideration>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        GroupConsideration group = (GroupConsideration)loaded.Considerations[0];

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Considerations[^1], Is.InstanceOf<TuningConsideration>(), "the default after the group");
            Assert.That(loaded.Considerations, Has.Count.EqualTo(2));
            Assert.That(group.Considerations[0], Is.InstanceOf<ExecutionHistoryConsideration>());
            Assert.That(group.Considerations, Has.Count.EqualTo(1), "the group gains no default of its own");
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

    [Test]
    public void LoadsAGroupedVetoWithNoWarning()
    {
        // The warning that an option's fixed values can never select it reads only the option's own list. It stops at
        // the first consideration that is not a Tuning, so a veto written inside a group loads with no warning.
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Disabled">
                                   <Consideration Type="Group">
                                       <Consideration Type="Tuning" Multiplier="0"/>
                                   </Consideration>
                               </Option>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationAndActionOption loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Calculate();

            Assert.That(loaded.Weight, Is.EqualTo(0f), "the grouped veto still vetoes");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }
}
