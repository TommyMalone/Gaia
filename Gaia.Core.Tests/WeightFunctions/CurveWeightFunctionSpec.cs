using Gaia.Core.Curves;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="CurveWeightFunction" />.</summary>
[TestFixture]
public class CurveWeightFunctionSpec
{
    [SetUp]
    public void BuildCurves()
    {
        _risingCurve = BuildStraightLine(0f, 4f);
        _fallingCurve = BuildStraightLine(1f, 0f);
    }

    private const float Tolerance = 0.0001f;

    private Curve _risingCurve;
    private Curve _fallingCurve;

    [Test]
    public void ReportsAnAddendFromItsAddendCurveAndLeavesTheMultiplierAlone()
    {
        CurveWeightFunction weightFunction = new() { AddendCurve = _risingCurve };

        WeightValues weights = weightFunction.CalcFloat(5f);

        Assert.Multiple(() =>
        {
            Assert.That(weights.Addend, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(weights.Multiplier, Is.EqualTo(1f));
        });
    }

    [Test]
    public void ReportsAMultiplierFromItsMultiplierCurveAndContributesNoAddend()
    {
        CurveWeightFunction weightFunction = new() { MultiplierCurve = _fallingCurve };

        WeightValues weights = weightFunction.CalcFloat(5f);

        Assert.Multiple(() =>
        {
            Assert.That(weights.Addend, Is.EqualTo(0f));
            Assert.That(weights.Multiplier, Is.EqualTo(0.5f).Within(Tolerance));
        });
    }

    [Test]
    public void UsesEachCurveForItsOwnChannel()
    {
        CurveWeightFunction weightFunction = new()
        {
            AddendCurve = _risingCurve,
            MultiplierCurve = _fallingCurve,
        };

        WeightValues weights = weightFunction.CalcFloat(5f);

        Assert.Multiple(() =>
        {
            Assert.That(weights.Addend, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(weights.Multiplier, Is.EqualTo(0.5f).Within(Tolerance));
        });
    }

    [Test]
    public void ReportsNeutralValuesWithNoCurves()
    {
        CurveWeightFunction weightFunction = new();

        Assert.That(weightFunction.CalcFloat(5f), Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void ReportsTheSameRankWhateverTheInput()
    {
        // Rank names a tier rather than a quantity, so it is configured rather than curved.
        CurveWeightFunction weightFunction = new()
        {
            AddendCurve = _risingCurve,
            Rank = 2f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(weightFunction.CalcFloat(0f).Rank, Is.EqualTo(2f));
            Assert.That(weightFunction.CalcFloat(5f).Rank, Is.EqualTo(2f));
            Assert.That(weightFunction.CalcFloat(10f).Rank, Is.EqualTo(2f));
        });
    }

    [Test]
    public void ConvertsAnIntAsAFloat()
    {
        CurveWeightFunction weightFunction = new() { AddendCurve = _risingCurve };

        Assert.That(weightFunction.CalcInt(5).Addend, Is.EqualTo(2f).Within(Tolerance));
    }

    [Test]
    public void RefusesInputTypesItHasNoConversionFor()
    {
        CurveWeightFunction weightFunction = new() { AddendCurve = _risingCurve };

        Assert.Multiple(() =>
        {
            Assert.That(() => weightFunction.CalcBool(true), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => weightFunction.CalcString("Enemy"), Throws.TypeOf<NotSupportedException>());
        });
    }

    /// <summary>Builds a curve running straight from one value to another across inputs of 0 to 10.</summary>
    private static Curve BuildStraightLine(float valueAtZero, float valueAtTen)
    {
        Curve curve = new();

        CurveKeyframe start = new(0f, valueAtZero);
        CurveKeyframe end = new(10f, valueAtTen);

        curve.AddKeyframe(start);
        curve.AddKeyframe(end);

        curve.SetLinearTangents();

        return curve;
    }
}
