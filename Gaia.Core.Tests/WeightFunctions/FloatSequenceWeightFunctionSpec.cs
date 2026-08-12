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
