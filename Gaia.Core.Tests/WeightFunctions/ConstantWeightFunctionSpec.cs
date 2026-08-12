using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="ConstantWeightFunction" />.</summary>
[TestFixture]
public class ConstantWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _configuredWeights = new WeightValues(3f, 0.5f, 2f);

        _weightFunction = new ConstantWeightFunction { Weights = _configuredWeights };
    }

    private WeightValues _configuredWeights;

    private ConstantWeightFunction _weightFunction;

    [Test]
    public void ReportsItsConfiguredValuesWhateverTheInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateInt(4), Is.EqualTo(_configuredWeights));
            Assert.That(_weightFunction.CalculateBool(true), Is.EqualTo(_configuredWeights));
            Assert.That(_weightFunction.CalculateBool(false), Is.EqualTo(_configuredWeights));
            Assert.That(_weightFunction.CalculateFloat(0f), Is.EqualTo(_configuredWeights));
            Assert.That(_weightFunction.CalculateFloat(9999f), Is.EqualTo(_configuredWeights));
            Assert.That(_weightFunction.CalculateString("anything"), Is.EqualTo(_configuredWeights));
        });
    }
}
