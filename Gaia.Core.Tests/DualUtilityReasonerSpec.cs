using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Reasoner;
using Gaia.Core.Services.Random;
using Gaia.Core.Temp;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers DualUtilityReasoner's four selection steps: discard options with no weight, keep only the highest rank
///     tier, discard survivors that fall too far below the best weight, then choose among what is left at random in
///     proportion to weight. Each option's numbers come from a stub consideration set by hand, and the random source is
///     fixed, so each test pins down one step rather than sampling a distribution.
/// </summary>
/// <remarks>
///     Each option's numbers come from a <see cref="StubConsideration" /> set by hand, and the random source is fixed,
///     so each test pins down one step rather than sampling a distribution.
/// </remarks>
[TestFixture]
public class DualUtilityReasonerSpec
{
    [SetUp]
    public void BuildReasoner()
    {
        _executed = new List<string>();
        _dualUtilityReasoner = new DualUtilityReasoner();

        _firstOptionConsideration = new StubConsideration();
        _secondOptionConsideration = new StubConsideration();

        // A neutral Tuning contributes nothing, but its presence stops Init from adding the default one with an
        // addend of 1. That keeps each option's weight exactly what its stub consideration reports.
        _firstOptionTuning = new TuningConsideration(WeightValues.Neutral);
        _secondOptionTuning = new TuningConsideration(WeightValues.Neutral);

        _vetoedWithALargeAddend = new WeightValues(50f, 0f, float.MinValue);

        _heavyWeight = new WeightValues(3f, 1f, float.MinValue);
        _lightWeight = new WeightValues(1f, 1f, float.MinValue);

        _highRankButLight = new WeightValues(1f, 1f, 1f);
        _lowRankButHeavy = new WeightValues(100f, 1f, 0f);
    }

    private List<string> _executed;

    private StubConsideration _firstOptionConsideration;
    private StubConsideration _secondOptionConsideration;

    private TuningConsideration _firstOptionTuning;
    private TuningConsideration _secondOptionTuning;

    private WeightValues _vetoedWithALargeAddend;

    private WeightValues _heavyWeight;
    private WeightValues _lightWeight;

    private WeightValues _highRankButLight;
    private WeightValues _lowRankButHeavy;

    private DualUtilityReasoner _dualUtilityReasoner;

    [Test]
    public void SkipsOptionsThatAreVetoed()
    {
        _firstOptionConsideration.Set(_vetoedWithALargeAddend);
        _secondOptionConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Vetoed", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Valid", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Valid.Select", "Valid.Update" }));
    }

    [Test]
    public void SelectsNothingWhenEveryOptionIsVetoed()
    {
        _firstOptionConsideration.Set(_vetoedWithALargeAddend);
        _secondOptionConsideration.Set(_vetoedWithALargeAddend);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("First", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Second", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Is.Empty);
            Assert.That(_dualUtilityReasoner.SelectedOption, Is.Null);
            Assert.That(_dualUtilityReasoner.IsDone(), Is.True);
        });
    }

    [Test]
    public void IgnoresLowerRanksNoMatterHowMuchTheyWeigh()
    {
        _firstOptionConsideration.Set(_highRankButLight);
        _secondOptionConsideration.Set(_lowRankButHeavy);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("HighRank", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("LowRank", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "HighRank.Select", "HighRank.Update" }));
    }

    [Test]
    public void DiscardsSurvivorsFarBelowTheBestWeight()
    {
        _firstOptionConsideration.Set(_heavyWeight);
        _secondOptionConsideration.Set(_lightWeight);

        // The 0.95 roll is chosen so that the answer changes if step 3 is skipped, which is what gives this test its
        // teeth.
        // With screening, the threshold is 3 * 0.5 = 1.5, Light's weight of 1 is dropped, and the total falls to 3.  A
        // roll of 0.95 lands at 2.85, inside Heavy's share. Without screening, Light survives, the total is 4, and the
        // same roll lands at 3.8, which is Light's. A roll of 0 would take Heavy either way and so would pass even if
        // screening were deleted outright.
        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.95f);

        // Screen light from the valid options.
        _dualUtilityReasoner.WeightScreeningFactor = 0.5f;

        OptionBase heavy = TestHelpers.BuildOption("Heavy", _executed, _firstOptionConsideration, _firstOptionTuning);
        _dualUtilityReasoner.AddOption(heavy);

        OptionBase light = TestHelpers.BuildOption("Light", _executed, _secondOptionConsideration, _secondOptionTuning);
        _dualUtilityReasoner.AddOption(light);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Heavy.Select", "Heavy.Update" }));
    }

    [Test]
    public void LandsOnTheHeavierOptionForALowRoll()
    {
        _firstOptionConsideration.Set(_heavyWeight);
        _secondOptionConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Heavy", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Light", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Heavy.Select", "Heavy.Update" }));
    }

    [Test]
    public void LandsOnTheLighterOptionForAHighRoll()
    {
        _firstOptionConsideration.Set(_heavyWeight);
        _secondOptionConsideration.Set(_lightWeight);

        // Weights of 3 and 1 give a total of 4, so Heavy owns the roll range [0, 3) and Light owns [3, 4). A roll of
        // 0.9 lands at 3.6, inside Light's share. Together with the low-roll test this brackets the boundary at 3 from
        // both sides, which is what shows selection actually walks the accumulated weights instead of always taking the
        // first candidate.
        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.9f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Heavy", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Light", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Light.Select", "Light.Update" }));
    }

    [Test]
    public void KeepsTheSameOptionSelectedAcrossTicks()
    {
        _firstOptionConsideration.Set(_heavyWeight);
        _secondOptionConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Heavy", _executed, _firstOptionConsideration,
            _firstOptionTuning));
        _dualUtilityReasoner.AddOption(TestHelpers.BuildOption("Light", _executed, _secondOptionConsideration,
            _secondOptionTuning));

        _dualUtilityReasoner.Update();

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Heavy.Select", "Heavy.Update", "Heavy.Update" }));
    }
}
