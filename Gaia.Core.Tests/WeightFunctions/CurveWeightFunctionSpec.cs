using Gaia.Core.Curves;
using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
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

        WeightValues weights = weightFunction.CalculateFloat(5f);

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

        WeightValues weights = weightFunction.CalculateFloat(5f);

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

        WeightValues weights = weightFunction.CalculateFloat(5f);

        Assert.Multiple(() =>
        {
            Assert.That(weights.Addend, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(weights.Multiplier, Is.EqualTo(0.5f).Within(Tolerance));
        });
    }

    [Test]
    public void ClampsAMultiplierCurveAtTheVeto()
    {
        Curve divingCurve = BuildStraightLine(1f, -1f);

        CurveWeightFunction weightFunction = new() { MultiplierCurve = divingCurve };

        Assert.Multiple(() =>
        {
            Assert.That(weightFunction.CalculateFloat(0f).Multiplier, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(weightFunction.CalculateFloat(10f).Multiplier, Is.EqualTo(0f),
                "below 0 clamps to the veto rather than flipping the weight");
        });
    }

    [Test]
    public void ReportsNeutralValuesWithNoCurves()
    {
        CurveWeightFunction weightFunction = new();

        Assert.That(weightFunction.CalculateFloat(5f), Is.EqualTo(WeightValues.Neutral));
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
            Assert.That(weightFunction.CalculateFloat(0f).Rank, Is.EqualTo(2f));
            Assert.That(weightFunction.CalculateFloat(5f).Rank, Is.EqualTo(2f));
            Assert.That(weightFunction.CalculateFloat(10f).Rank, Is.EqualTo(2f));
        });
    }

    [Test]
    public void ConvertsAnIntAsAFloat()
    {
        CurveWeightFunction weightFunction = new() { AddendCurve = _risingCurve };

        Assert.That(weightFunction.CalculateInt(5).Addend, Is.EqualTo(2f).Within(Tolerance));
    }

    [Test]
    public void RefusesInputTypesItHasNoConversionFor()
    {
        CurveWeightFunction weightFunction = new() { AddendCurve = _risingCurve };

        Assert.Multiple(() =>
        {
            Assert.That(() => weightFunction.CalculateBool(true), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => weightFunction.CalculateString("Enemy"), Throws.TypeOf<NotSupportedException>());
        });
    }

    [Test]
    public void LoadsItsCurvesAndRank()
    {
        const string snippet = """
                               <WeightFunction Type="Curve" Rank="5">
                                   <AddendCurve>
                                       <Keyframe Input="0" Value="0"/>
                                       <Keyframe Input="10" Value="4"/>
                                   </AddendCurve>
                                   <MultiplierCurve>
                                       <Keyframe Input="0" Value="1"/>
                                   </MultiplierCurve>
                               </WeightFunction>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            WeightValues atTheTop = loaded.CalculateFloat(10f);

            Assert.That(atTheTop.Addend, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(atTheTop.Multiplier, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(atTheTop.Rank, Is.EqualTo(5f));
            Assert.That(logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void StraightensACurveMarkedLinear()
    {
        const string snippet = """
                               <WeightFunction Type="Curve">
                                   <AddendCurve Linear="true">
                                       <Keyframe Input="0" Value="0"/>
                                       <Keyframe Input="10" Value="4"/>
                                   </AddendCurve>
                               </WeightFunction>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            // The default flat tangents would ease through 0.625 here. The straight line passes through 1 exactly.
            Assert.That(loaded.CalculateFloat(2.5f).Addend, Is.EqualTo(1f).Within(Tolerance));
        });
    }

    [Test]
    public void WarnsAboutAMultiplierCurveAuthoredBelowZero()
    {
        const string snippet = """
                               <WeightFunction Type="Curve">
                                   <MultiplierCurve>
                                       <Keyframe Input="0" Value="1"/>
                                       <Keyframe Input="10" Value="-1"/>
                                   </MultiplierCurve>
                               </WeightFunction>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True, "a diving curve loads, clamped rather than refused");
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Warning guard.xml Line 2, the MultiplierCurve element dips below 0 and will be clamped to 0",
            }));
        });
    }

    [Test]
    public void RefusesACurveElementWithNoKeyframes()
    {
        const string snippet = """
                               <WeightFunction Type="Curve">
                                   <AddendCurve/>
                               </WeightFunction>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the AddendCurve element has no Keyframe elements",
            }));
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
