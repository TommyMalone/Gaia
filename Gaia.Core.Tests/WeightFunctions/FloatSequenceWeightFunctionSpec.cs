using Gaia.Core.Services.Random;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="FloatSequenceWeightFunction" />.</summary>
[TestFixture]
public class FloatSequenceWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _inRangeWeights = new WeightValues(2f, 1f, float.MinValue);

        _weightFunction = new FloatSequenceWeightFunction(WeightValues.Vetoed);

        FloatSequenceEntry tooCloseEntry = new(50f, WeightValues.Vetoed);
        FloatSequenceEntry inRangeEntry = new(300f, _inRangeWeights);

        _weightFunction.AddEntry(tooCloseEntry);
        _weightFunction.AddEntry(inRangeEntry);
    }

    private WeightValues _inRangeWeights;

    private FloatSequenceWeightFunction _weightFunction;

    [Test]
    public void ReportsTheFirstEntryTheInputFallsWithin()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(25f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(200f), Is.EqualTo(_inRangeWeights));
        });
    }

    [Test]
    public void ReportsMinimumThresholdAsExclusiveAndMaximumThresholdAsInclusive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(50f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(50.01f), Is.EqualTo(_inRangeWeights));
            Assert.That(_weightFunction.CalculateFloat(300f), Is.EqualTo(_inRangeWeights));
        });
    }

    [Test]
    public void ReportsTheDefaultAboveEveryThreshold()
    {
        Assert.That(_weightFunction.CalculateFloat(500f), Is.EqualTo(WeightValues.Vetoed));
    }

    [Test]
    public void ReportsTheDefaultWhenThereAreNoEntries()
    {
        FloatSequenceWeightFunction emptyWeightFunction = new(WeightValues.Vetoed);

        Assert.That(emptyWeightFunction.CalculateFloat(100f), Is.EqualTo(WeightValues.Vetoed));
    }

    [Test]
    public void LeavesFixedThresholdsAloneWhenTheOptionIsSelected()
    {
        // The fixture's entries were given a single threshold each, which is the common case, so selection changes
        // nothing about what they match.
        _weightFunction.RandomSource = new FixedRandomSource(0.9f);

        _weightFunction.Select();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(50f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(200f), Is.EqualTo(_inRangeWeights));
            Assert.That(_weightFunction.CalculateFloat(500f), Is.EqualTo(WeightValues.Vetoed));
        });
    }

    [Test]
    public void RollsAFreshThresholdWhenGivenARangeAndTheOptionIsSelected()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);
        weightFunction.RandomSource = new FixedRandomSource(0.9f);

        FloatSequenceEntry randomizedEntry = new(50f, 150f, _inRangeWeights);
        weightFunction.AddEntry(randomizedEntry);

        // Threshold sits at ThresholdMin until Select rolls it, so 100 falls past the entry.
        Assert.That(weightFunction.CalculateFloat(100f), Is.EqualTo(WeightValues.Vetoed));

        weightFunction.Select();

        // A roll of 0.9 puts the threshold at 140, so the same input now falls inside the entry.
        Assert.That(weightFunction.CalculateFloat(100f), Is.EqualTo(_inRangeWeights));
    }

    [Test]
    public void RejectsAnEntryOutOfOrder()
    {
        // The entries are tested first to last, so one added out of order would shadow everything after it.
        FloatSequenceEntry earlierEntry = new(100f, _inRangeWeights);

        Assert.That(() => _weightFunction.AddEntry(earlierEntry), Throws.ArgumentException);
    }

    [Test]
    public void AcceptsAnEntryAtTheSameThresholdAsTheLast()
    {
        // Equal thresholds are harmless. The earlier entry answers, and the later one is simply never reached.
        FloatSequenceEntry repeatedEntry = new(300f, WeightValues.Vetoed);

        Assert.That(() => _weightFunction.AddEntry(repeatedEntry), Throws.Nothing);
    }

    [Test]
    public void RejectsARolledRangeThatOverlapsTheEntryBefore()
    {
        // An entry rolling between 250 and 350 would sometimes land below the 300 entry and sometimes above it.
        FloatSequenceEntry overlappingEntry = new(250f, 350f, WeightValues.Vetoed);

        Assert.That(() => _weightFunction.AddEntry(overlappingEntry), Throws.ArgumentException);
    }

    [Test]
    public void RejectsAnEntryThatCouldRollBelowTheRangeBefore()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);

        FloatSequenceEntry rolledEntry = new(50f, 150f, _inRangeWeights);
        FloatSequenceEntry tooEarlyEntry = new(120f, 200f, WeightValues.Vetoed);
        FloatSequenceEntry lateEnoughEntry = new(150f, 200f, WeightValues.Vetoed);

        weightFunction.AddEntry(rolledEntry);

        Assert.Multiple(() =>
        {
            Assert.That(() => weightFunction.AddEntry(tooEarlyEntry), Throws.ArgumentException);
            Assert.That(() => weightFunction.AddEntry(lateEnoughEntry), Throws.Nothing);
        });
    }

    [Test]
    public void RejectsARangeWhoseMaximumIsBelowItsMinimum()
    {
        Assert.That(() => new FloatSequenceEntry(150f, 50f, _inRangeWeights), Throws.ArgumentException);
    }

    [Test]
    public void ReportsWhichEntriesRollTheirThreshold()
    {
        FloatSequenceEntry fixedEntry = new(100f, _inRangeWeights);
        FloatSequenceEntry randomizedEntry = new(50f, 150f, _inRangeWeights);

        Assert.Multiple(() =>
        {
            Assert.That(fixedEntry.IsRandomized, Is.False);
            Assert.That(randomizedEntry.IsRandomized, Is.True);
        });
    }
}
