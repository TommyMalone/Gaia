using Gaia.Core.Reasoners;
using Gaia.Core.Considerations;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers RuleBasedReasoner's selection logic, which walks its options in the order they were added and takes the
///     first valid one, where valid means a weight above zero. Weight and rank play no part beyond that, which is what
///     separates this reasoner from the dual utility one. Considerations are stubbed rather than queried from a world,
///     so a failure here points at the reasoner rather than at a consideration. Expected logs are grouped a tick per
///     line.
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

        _ruleBasedReasoner = new RuleBasedReasoner();
        _ruleBasedReasoner.Enable();

        // Both options start valid. Each test changes what its considerations report before ticking.
        _firstConsideration = new StubConsideration(WeightValues.Neutral);
        _secondConsideration = new StubConsideration(WeightValues.Neutral);

        RecordAction firstRecordAction = new("First", _executed);
        RecordAction secondRecordAction = new("Second", _executed);

        StubOption firstOption = new([_firstConsideration], [firstRecordAction]);
        StubOption secondOption = new([_secondConsideration], [secondRecordAction]);

        _ruleBasedReasoner.AddOption(firstOption);
        _ruleBasedReasoner.AddOption(secondOption);

        _lightWeight = new WeightValues(1f, 1f, float.MinValue);
        _heavyWeight = new WeightValues(100f, 1f, float.MinValue);

        _lowRank = new WeightValues(1f, 1f, 0f);
        _highRank = new WeightValues(1f, 1f, 10f);
    }

    private List<string> _executed;

    private RuleBasedReasoner _ruleBasedReasoner;

    private StubConsideration _firstConsideration;
    private StubConsideration _secondConsideration;

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
            Assert.That(_ruleBasedReasoner.IsDone(), Is.True);
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
    public void KeepsTheSameOptionSelectedAcrossTicks()
    {
        // Select and Deselect are transitions; Update is the per-tick call. An option that stays valid across
        // ticks is selected once and updated every tick.
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
        // updated on the tick that swaps them.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Second.Select", "Second.Update",
            "Second.Deselect", "First.Select", "First.Update",
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
}
