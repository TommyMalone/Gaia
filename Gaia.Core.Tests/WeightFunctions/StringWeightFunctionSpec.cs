using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="StringWeightFunction" />.</summary>
[TestFixture]
public class StringWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _enemyWeights = new WeightValues(2f, 1f, float.MinValue);

        _weightFunction = new StringWeightFunction(WeightValues.Vetoed);
        _weightFunction.AddEntry("Enemy", _enemyWeights);
    }

    private WeightValues _enemyWeights;

    private StringWeightFunction _weightFunction;

    [Test]
    public void ReportsTheValuesOfTheEntryThatMatched()
    {
        Assert.That(_weightFunction.CalculateString("Enemy"), Is.EqualTo(_enemyWeights));
    }

    [Test]
    public void ReportsTheDefaultWhenNothingMatched()
    {
        Assert.That(_weightFunction.CalculateString("Ally"), Is.EqualTo(WeightValues.Vetoed));
    }

    [Test]
    public void IgnoresCaseWhileMatching()
    {
        Assert.That(_weightFunction.CalculateString("enemy"), Is.EqualTo(_enemyWeights));
    }

    [Test]
    public void ReplacesAnEntryConfiguredTwice()
    {
        WeightValues replacementWeights = new(9f, 1f, float.MinValue);

        _weightFunction.AddEntry("Enemy", replacementWeights);

        Assert.That(_weightFunction.CalculateString("Enemy"), Is.EqualTo(replacementWeights));
    }

    [Test]
    public void ReportsTheDefaultWhenThereAreNoEntries()
    {
        StringWeightFunction emptyWeightFunction = new(WeightValues.Vetoed);

        Assert.That(emptyWeightFunction.CalculateString("Enemy"), Is.EqualTo(WeightValues.Vetoed));
    }
}
