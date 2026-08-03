using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="WeightValues" /> as a value, its equality, its printing and its two named constants.</summary>
/// <remarks>How the three values steer a decision is covered through the consideration and reasoner specs.</remarks>
[TestFixture]
public class WeightValuesSpec
{
    [SetUp]
    public void BuildReports()
    {
        _report = new WeightValues(3f, 2f, 1f);
        _sameReport = new WeightValues(3f, 2f, 1f);
    }

    private WeightValues _report;
    private WeightValues _sameReport;

    [Test]
    public void EqualsAnotherReportOfTheSameThreeValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_report == _sameReport, Is.True);
            Assert.That(_report != _sameReport, Is.False);
        });

        WeightValues higherRank = new(_report.Addend, _report.Multiplier, 5f);

        Assert.Multiple(() =>
        {
            Assert.That(_report == higherRank, Is.False);
            Assert.That(_report != higherRank, Is.True);
            Assert.That(_report.Rank, Is.EqualTo(1f));
            Assert.That(higherRank.Rank, Is.EqualTo(5f));
        });
    }

    [Test]
    public void EqualsTheSameReportArrivingBoxed()
    {
        object boxedSameReport = new WeightValues(3f, 2f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(_report.Equals(boxedSameReport), Is.True);

            // A value of another type is never equal, even a bare float matching the addend.
            Assert.That(_report.Equals(3f), Is.False);
        });
    }

    [Test]
    public void HashesEqualReportsAlike()
    {
        Assert.That(_report.GetHashCode(), Is.EqualTo(_sameReport.GetHashCode()));
    }

    [Test]
    public void PrintsAllThreeValuesByName()
    {
        Assert.That(_report.ToString(), Is.EqualTo("Addend 3, Multiplier 2, Rank 1"));
    }

    [Test]
    public void ContributesNothingAsNeutral()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WeightValues.Neutral.Addend, Is.EqualTo(0f), "the summing identity");
            Assert.That(WeightValues.Neutral.Multiplier, Is.EqualTo(1f), "the multiplying identity");
            Assert.That(WeightValues.Neutral.Rank, Is.EqualTo(float.MinValue), "beneath every real rank");
        });
    }

    [Test]
    public void VetoesOnlyThroughItsMultiplierAsVetoed()
    {
        // Everything but the multiplier stays at the identity, so a veto neither raises nor outranks anything.
        Assert.Multiple(() =>
        {
            Assert.That(WeightValues.Vetoed.Multiplier, Is.EqualTo(0f));
            Assert.That(WeightValues.Vetoed.Addend, Is.EqualTo(WeightValues.Neutral.Addend));
            Assert.That(WeightValues.Vetoed.Rank, Is.EqualTo(WeightValues.Neutral.Rank));
        });
    }
}