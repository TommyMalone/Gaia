using Gaia.Core.Considerations;
using Gaia.Core.Reasoner;
using Gaia.Core.Services.Random;
using Gaia.Core.Temp;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers DualUtilityReasoner's four selection steps: discard options with no weight, keep only the highest rank
///     tier, discard survivors that fall too far below the best weight, then choose among what is left at random in
///     proportion to weight. Each option's numbers come from a stub consideration set by hand, and the random source is
///     fixed, so each test pins down one step rather than sampling a distribution. Expected logs are grouped a tick per
///     line.
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
        _dualUtilityReasoner.Enable();

        // A roll of 0 always lands on the first surviving option. A test whose answer has to change when the step it
        // covers is skipped sets its own roll instead.
        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        // A neutral TuningConsideration contributes nothing, but its presence stops Init from adding the default one
        // with an addend of 1. That keeps each option's weight exactly what its StubConsideration reports.
        // TuningConsideration holds no per-option state, so one instance can sit in every option.
        TuningConsideration neutralTuning = new(WeightValues.Neutral);

        _firstConsideration = new StubConsideration(WeightValues.Neutral);
        _secondConsideration = new StubConsideration(WeightValues.Neutral);

        RecordAction firstRecordAction = new("First", _executed);
        RecordAction secondRecordAction = new("Second", _executed);

        StubOption firstOption = new([_firstConsideration, neutralTuning], [firstRecordAction]);
        StubOption secondOption = new([_secondConsideration, neutralTuning], [secondRecordAction]);

        _dualUtilityReasoner.AddOption(firstOption);
        _dualUtilityReasoner.AddOption(secondOption);

        _vetoedWithALargeAddend = new WeightValues(50f, 0f, float.MinValue);

        _heavyWeight = new WeightValues(3f, 1f, float.MinValue);
        _lightWeight = new WeightValues(1f, 1f, float.MinValue);

        _highRankButLight = new WeightValues(1f, 1f, 1f);
        _lowRankButHeavy = new WeightValues(100f, 1f, 0f);
    }

    private List<string> _executed;

    private DualUtilityReasoner _dualUtilityReasoner;

    private StubConsideration _firstConsideration;
    private StubConsideration _secondConsideration;

    private WeightValues _vetoedWithALargeAddend;

    private WeightValues _heavyWeight;
    private WeightValues _lightWeight;

    private WeightValues _highRankButLight;
    private WeightValues _lowRankButHeavy;

    [Test]
    public void SkipsOptionsThatAreVetoed()
    {
        // Step 1. A multiplier of 0 zeroes the weight no matter how large the addend is.
        _firstConsideration.Set(_vetoedWithALargeAddend);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void SelectsNothingWhenEveryOptionIsVetoed()
    {
        _firstConsideration.Set(_vetoedWithALargeAddend);
        _secondConsideration.Set(_vetoedWithALargeAddend);

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
        // Step 2. Rank is a hard tier boundary, so the option weighing a hundred times more never gets considered.
        // The 0.5 roll is chosen so that the answer changes if this step is skipped. With rank elimination the total
        // weight is First's 1 and any roll selects First. Without it the total is 101, and a roll of 0.5 lands at
        // 50.5, inside Second's share.
        _firstConsideration.Set(_highRankButLight);
        _secondConsideration.Set(_lowRankButHeavy);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.5f);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void DiscardsSurvivorsFarBelowTheBestWeight()
    {
        // Step 3. The 0.95 roll is chosen so that the answer changes if this step is skipped. With a
        // WeightScreeningFactor of 0.5, the threshold is 3 * 0.5 = 1.5, Second's weight of 1 is dropped, and the
        // total falls to 3. A roll of 0.95 lands at 2.85, inside First's share. Without
        // screening, Second survives, the total is 4, and the same roll lands at 3.8, which is Second's. A roll of 0
        // would take First either way and so would pass even if screening were deleted outright.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.95f);
        _dualUtilityReasoner.WeightScreeningFactor = 0.5f;

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void LandsOnTheHeavierOptionForALowRoll()
    {
        // Step 4. Weights of 3 and 1 give a total of 4. A roll of 0 falls inside First's share of [0, 3).
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void LandsOnTheLighterOptionForAHighRoll()
    {
        // Step 4 again. The same two options, so First owns the roll range [0, 3) and Second owns [3, 4). A roll of
        // 0.9 lands at 3.6, inside Second's share. A roll landing on either side of the boundary at 3 is what shows
        // selection actually walks the accumulated weights instead of always taking the first candidate.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.9f);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void KeepsTheSameOptionSelectedAcrossTicks()
    {
        // Selection is re-run every tick. With nothing changing and a fixed roll the same option wins again, so it
        // is updated a second time rather than torn down and restarted.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Update",
            // @formatter:on
        }));
    }
}
