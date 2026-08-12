using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="BooleanWeightFunction" />.</summary>
[TestFixture]
public class BooleanWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _trueWeights = new WeightValues(2f, 1f, float.MinValue);
        _falseWeights = WeightValues.Vetoed;

        _weightFunction = new BooleanWeightFunction(_trueWeights, _falseWeights);
    }

    private WeightValues _trueWeights;
    private WeightValues _falseWeights;

    private BooleanWeightFunction _weightFunction;

    [Test]
    public void ReportsADifferentSetOfValuesForEachAnswer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateBool(true), Is.EqualTo(_trueWeights));
            Assert.That(_weightFunction.CalculateBool(false), Is.EqualTo(_falseWeights));
        });
    }

    [Test]
    public void RefusesAnythingButABool()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _weightFunction.CalculateFloat(1f), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => _weightFunction.CalculateInt(1), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => _weightFunction.CalculateString("true"), Throws.TypeOf<NotSupportedException>());
        });
    }
}
