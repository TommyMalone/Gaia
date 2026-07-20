using Gaia.Core.Reasoner;
using Gaia.Core.Temp;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers RuleBasedReasoner's selection logic, which walks its options in the order they were added and takes the
///     first valid one, where valid means a weight above zero. Weight and rank play no part beyond that, which is what
///     separates this reasoner from the dual utility one. Considerations are stubbed rather than queried from a world, so
///     a failure here points at the reasoner rather than at a consideration.
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

        _firstOptionConsideration = new StubConsideration();
        _secondOptionConsideration = new StubConsideration();

        _lightWeight = new WeightValues(1f, 1f, float.MinValue);
        _heavyWeight = new WeightValues(100f, 1f, float.MinValue);

        _lowRank = new WeightValues(1f, 1f, 0f);
        _highRank = new WeightValues(1f, 1f, 10f);
    }

    private List<string> _executed;

    private StubConsideration _firstOptionConsideration;
    private StubConsideration _secondOptionConsideration;

    private WeightValues _lightWeight;
    private WeightValues _heavyWeight;

    private WeightValues _lowRank;
    private WeightValues _highRank;

    private RuleBasedReasoner _ruleBasedReasoner;

    [Test]
    public void SelectsTheFirstValidOption()
    {
        _firstOptionConsideration.Pass();
        _secondOptionConsideration.Pass();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void SkipsOptionsThatAreVetoed()
    {
        // A multiplier of 0 zeroes the weight, so the first option is not a candidate at all.
        _firstOptionConsideration.Veto();
        _secondOptionConsideration.Pass();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void SelectsNothingWhenEveryOptionIsVetoed()
    {
        _firstOptionConsideration.Veto();
        _secondOptionConsideration.Veto();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

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
        // Both are valid, and the second outweighs the first by two orders of magnitude. A dual utility reasoner
        // would almost always take Second; this one takes First because it was added first.
        _firstOptionConsideration.Set(_lightWeight);
        _secondOptionConsideration.Set(_heavyWeight);

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void IgnoresRankWhenSeveralOptionsAreValid()
    {
        // Rank is a hard tier boundary for DualUtilityReasoner. Here it carries no meaning at all. Both considerations
        // contribute the same addend, so rank is the only difference between the two options.
        _firstOptionConsideration.Set(_lowRank);
        _secondOptionConsideration.Set(_highRank);

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void KeepsTheSameOptionSelectedAcrossTicks()
    {
        // Select and Deselect are transitions; Update is the per-frame call. An option that stays valid across
        // ticks is selected once and updated every tick.
        _firstOptionConsideration.Pass();
        _secondOptionConsideration.Pass();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();
        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update", "First.Update" }));
    }

    [Test]
    public void SwitchesToAnEarlierOptionWhenItBecomesValid()
    {
        _firstOptionConsideration.Veto();
        _secondOptionConsideration.Pass();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        _firstOptionConsideration.Pass();

        _ruleBasedReasoner.Update();

        // The outgoing option is deselected before the incoming one is selected, and only the incoming option is
        // updated on the tick that swaps them.
        Assert.That(_executed,
            Is.EqualTo(new[] { "Second.Select", "Second.Update", "Second.Deselect", "First.Select", "First.Update" }));
    }

    [Test]
    public void FallsThroughToALaterOptionWhenTheRunningOneIsVetoed()
    {
        _firstOptionConsideration.Pass();
        _secondOptionConsideration.Pass();

        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration));
        _ruleBasedReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration));

        _ruleBasedReasoner.Update();

        _firstOptionConsideration.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed,
            Is.EqualTo(new[] { "First.Select", "First.Update", "First.Deselect", "Second.Select", "Second.Update" }));
    }
}
