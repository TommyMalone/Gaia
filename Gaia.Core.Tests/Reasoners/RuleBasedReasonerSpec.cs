using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Time;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Reasoners;

/// <summary>
///     Covers <see cref="RuleBasedReasoner" />'s selection logic, which walks its options in the order they were
///     added and takes the first valid one, where valid means a weight above zero. Weight and rank play no part beyond
///     that, which is what separates this reasoner from <see cref="DualUtilityReasoner" />.
/// </summary>
/// <remarks>
///     Considerations are stubbed rather than queried from a world, so a failure here points at the reasoner rather
///     than at a consideration.
/// </remarks>
[TestFixture]
public class RuleBasedReasonerSpec
{
    [SetUp]
    public void BuildReasoner()
    {
        _executed = new List<string>();
        _logSink = new RecordingLogSink();

        _ruleBasedReasoner = new RuleBasedReasoner();
        _ruleBasedReasoner.Enable();

        // Both options start valid. Each test changes what its considerations report before the cycle runs.
        _firstConsideration = new StubConsideration(WeightValues.Neutral);
        _secondConsideration = new StubConsideration(WeightValues.Neutral);

        RecordAction firstRecordAction = new("First", _executed, false);
        RecordAction secondRecordAction = new("Second", _executed, false);

        StubOption firstOption = new([_firstConsideration], [firstRecordAction]);
        StubOption secondOption = new([_secondConsideration], [secondRecordAction]);

        _ruleBasedReasoner.AddOption(firstOption);
        _ruleBasedReasoner.AddOption(secondOption);

        // Done the moment it is selected, for the tests about an option that finishes.
        _instantlyDoneAction = new RecordAction("InstantlyDone", _executed);

        _lightWeight = new WeightValues(1f, 1f, float.MinValue);
        _heavyWeight = new WeightValues(100f, 1f, float.MinValue);

        _lowRank = new WeightValues(1f, 1f, 0f);
        _highRank = new WeightValues(1f, 1f, 10f);
    }

    private List<string> _executed;
    private RecordingLogSink _logSink;

    private RuleBasedReasoner _ruleBasedReasoner;

    private StubConsideration _firstConsideration;
    private StubConsideration _secondConsideration;

    private RecordAction _instantlyDoneAction;

    private WeightValues _lightWeight;
    private WeightValues _heavyWeight;

    private WeightValues _lowRank;
    private WeightValues _highRank;

    [Test]
    public void SelectsTheFirstValidOption()
    {
        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void SkipsOptionsThatAreVetoed()
    {
        // A multiplier of 0 zeroes the weight, so the first option is not a candidate at all.
        _firstConsideration.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void SelectsNothingWhenEveryOptionIsVetoed()
    {
        _firstConsideration.Veto();
        _secondConsideration.Veto();

        _ruleBasedReasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Is.Empty);
            Assert.That(_ruleBasedReasoner.SelectedOption, Is.Null);
            Assert.That(_ruleBasedReasoner.IsDone, Is.False,
                "nothing eligible is not completion, since a scoring reasoner never runs out of work");
        });
    }

    [Test]
    public void IgnoresWeightWhenSeveralOptionsAreValid()
    {
        // Both are valid, and the second outweighs the first by two orders of magnitude. A DualUtilityReasoner would
        // almost always take Second, whereas this one takes First because it was added first.
        _firstConsideration.Set(_lightWeight);
        _secondConsideration.Set(_heavyWeight);

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void IgnoresRankWhenSeveralOptionsAreValid()
    {
        // Rank is a hard tier boundary for DualUtilityReasoner. Here it carries no meaning at all. Both considerations
        // contribute the same addend, so rank is the only difference between the two options.
        _firstConsideration.Set(_lowRank);
        _secondConsideration.Set(_highRank);

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void KeepsTheSameOptionSelectedAcrossCycles()
    {
        // Select and Deselect are transitions and Update is the per-cycle call. An option whose actions are still
        // running and which stays valid across cycles is selected once and updated every cycle.
        _ruleBasedReasoner.Update();
        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void SwitchesToAnEarlierOptionWhenItBecomesValid()
    {
        _firstConsideration.Veto();

        _ruleBasedReasoner.Update();

        _firstConsideration.Pass();

        _ruleBasedReasoner.Update();

        // The outgoing option is deselected before the incoming one is selected, and only the incoming option is
        // updated on the cycle that swaps them.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Second.Select", "Second.Update",
            "Second.Deselect", "First.Select", "First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void KeepsAFinishedOptionSelectedUntilAConsiderationVetoesIt()
    {
        // The reasoner takes no notice of the option's actions finishing. An action that is done the moment it runs
        // leaves its option selected, being updated every cycle, the same as one that never finishes.
        StubOption instantlyDoneOption = new([], [_instantlyDoneAction]);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(instantlyDoneOption);
        reasoner.Enable();

        reasoner.Update();
        reasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "InstantlyDone.Select", "InstantlyDone.Update",
            "InstantlyDone.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void LetsAFinishedOptionEndItselfThroughItsConsiderations()
    {
        // The Is Done pattern, cited on ExecutionHistoryConsideration. FinishedWeightFunction
        // vetoes once the option IsDone, which is what deselects it. With nothing keeping it out afterwards, it is
        // first valid again on the cycle after and starts over. A cooldown on SinceLastRunWeightFunction is what would
        // hold it back.
        ManualTimeSource timeSource = new();
        ConstantWeightFunction vetoWhenFinished = new() { Weights = WeightValues.Vetoed };

        ExecutionHistoryConsideration endsWhenFinished = new()
        {
            TimeSource = timeSource,
            FinishedWeightFunction = vetoWhenFinished,
        };

        RecordAction fallbackAction = new("Fallback", _executed, false);

        StubOption instantlyDoneOption = new([endsWhenFinished], [_instantlyDoneAction]);
        StubOption fallbackOption = new([], [fallbackAction]);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(instantlyDoneOption);
        reasoner.AddOption(fallbackOption);
        reasoner.Enable();

        reasoner.Update();
        reasoner.Update();
        reasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "InstantlyDone.Select", "InstantlyDone.Update",
            "InstantlyDone.Deselect", "Fallback.Select", "Fallback.Update",
            "Fallback.Deselect", "InstantlyDone.Select", "InstantlyDone.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void FallsThroughToALaterOptionWhenTheRunningOneIsVetoed()
    {
        _ruleBasedReasoner.Update();

        _firstConsideration.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Deselect", "Second.Select", "Second.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void LoadsItsOptionsAndRunsTheFirstValidOne()
    {
        const string snippet = """
                               <Reasoner Type="RuleBased">
                                   <Option Type="ConsiderationAndAction" Name="Patrol">
                                       <Consideration Type="Tuning" Multiplier="0"/>
                                   </Option>
                                   <Option Type="ConsiderationAndAction" Name="Idle"/>
                               </Reasoner>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        AiString idleName = "Idle";
        RuleBasedReasoner loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.Empty, "loading reports nothing");

            loaded.Enable();
            loaded.Update();

            // The loaded tuning vetoes Patrol, so the first valid rule is Idle.
            Assert.That(loaded.SelectedOption, Is.Not.Null);
            Assert.That(loaded.SelectedOption!.Name, Is.EqualTo(idleName));
        });
    }
}
