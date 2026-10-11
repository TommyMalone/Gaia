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
        _logSink = new RecordingLogSink();
        _risingCurve = BuildStraightLine(0f, 4f);
        _fallingCurve = BuildStraightLine(1f, 0f);
    }

    private RecordingLogSink _logSink;

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
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            WeightValues atTheTop = loaded.CalculateFloat(10f);

            Assert.That(atTheTop.Addend, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(atTheTop.Multiplier, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(atTheTop.Rank, Is.EqualTo(5f));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LoadsEveryAttributeItsDescriptionNames()
    {
        // The snippet spells every name as the description spells it, so it loads only if OnInit reads by the same
        // names the description holds, at the positions it holds them.
        ComponentDescription description = CurveWeightFunction.Description;
        string rank = description.Attributes[0].Spelling;
        SlotDescription addendCurve = description.Slots[0];
        SlotDescription multiplierCurve = description.Slots[1];
        string linear = addendCurve.Inline!.Attributes[0].Spelling;
        SlotDescription keyframe = addendCurve.Inline.Slots[0];
        string input = keyframe.Inline!.Attributes[0].Spelling;
        string value = keyframe.Inline.Attributes[1].Spelling;
        string inTangent = keyframe.Inline.Attributes[2].Spelling;
        string outTangent = keyframe.Inline.Attributes[3].Spelling;

        string snippet = $"""
                          <WeightFunction Type="Curve" {rank}="5">
                              <{addendCurve.Spelling}>
                                  <{keyframe.Spelling} {input}="0" {value}="0" {outTangent}="1"/>
                                  <{keyframe.Spelling} {input}="10" {value}="4" {inTangent}="2"/>
                              </{addendCurve.Spelling}>
                              <{multiplierCurve.Spelling} {linear}="true">
                                  <{keyframe.Spelling} {input}="0" {value}="1"/>
                                  <{keyframe.Spelling} {input}="10" {value}="0"/>
                              </{multiplierCurve.Spelling}>
                          </WeightFunction>
                          """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
            Assert.That(loaded.Rank, Is.EqualTo(5f));
            Assert.That(loaded.AddendCurve!.Keyframes[0].OutTangent, Is.EqualTo(1f));
            Assert.That(loaded.AddendCurve.Keyframes[1].InTangent, Is.EqualTo(2f));
            Assert.That(loaded.CalculateFloat(5f).Multiplier, Is.EqualTo(0.5f).Within(Tolerance),
                "the multiplier curve is straight, so it passes through the midpoint");
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
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            // A Keyframe element loaded without InTangent or OutTangent carries tangents of 0, and without Linear the
            // ease they give would pass through 0.625 here. The straight line passes through 1 exactly.
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
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True, "a diving curve loads, clamped rather than refused");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Warning guard.xml Line 2, the MultiplierCurve element dips below 0 and will be clamped to 0",
            }));
        });
    }

    [Test]
    public void RefusesACurveThatRepeatsAnInput()
    {
        const string snippet = """
                               <WeightFunction Type="Curve">
                                   <AddendCurve>
                                       <Keyframe Input="0" Value="0"/>
                                       <Keyframe Input="5" Value="2"/>
                                       <Keyframe Input="5" Value="4"/>
                                   </AddendCurve>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CurveWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 5, the Keyframe element repeats the input 5 of the Keyframe on line 4",
            }));
        });
    }

    [Test]
    public void RefusesAnAttributeOnAKeyframe()
    {
        // A keyframe is an inline element inside an inline curve element, two levels below the function, and the
        // check enters both with their own descriptions.
        const string snippet = """
                               <WeightFunction Type="Curve">
                                   <AddendCurve>
                                       <Keyframe Input="0" Value="0" Ranck="1"/>
                                   </AddendCurve>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool wasCreated =
            creationData.Factories.WeightFunctions.TryCreate(creationData, out WeightFunctionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Keyframe element does not take the Ranck attribute",
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
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the short slot inside the inline curve element after Init returns true, so the test loads
        // through the factory rather than calling Init.
        bool wasCreated =
            creationData.Factories.WeightFunctions.TryCreate(creationData, out WeightFunctionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the AddendCurve element has no Keyframe element",
            }));
        });
    }

    [TestCase("Rank=\"5\"", "Rank=\"high\"",
        "Line 1, the WeightFunction element's Rank is not a number, it reads \"high\"")]
    [TestCase("<MultiplierCurve>", "<MultiplierCurve Linear=\"sometimes\">",
        "Line 6, the MultiplierCurve element's Linear is not true or false, it reads \"sometimes\"")]
    [TestCase("<Keyframe Input=\"10\"", "<Point Input=\"10\"",
        "Line 4, the AddendCurve element does not take Point elements")]
    [TestCase("Input=\"0\" Value=\"0\"", "Input=\"low\" Value=\"0\"",
        "Line 3, the Keyframe element's Input is not a number, it reads \"low\"")]
    [TestCase("Value=\"4\"", "Value=\"high\"",
        "Line 4, the Keyframe element's Value is not a number, it reads \"high\"")]
    [TestCase("InTangent=\"0\"", "InTangent=\"steep\"",
        "Line 3, the Keyframe element's InTangent is not a number, it reads \"steep\"")]
    [TestCase("OutTangent=\"1\"", "OutTangent=\"steep\"",
        "Line 3, the Keyframe element's OutTangent is not a number, it reads \"steep\"")]
    [TestCase("Linear=\"false\"", "Linear=\"sometimes\"",
        "Line 2, the AddendCurve element's Linear is not true or false, it reads \"sometimes\"")]
    public void RefusesEachMalformedPieceOfACurve(string piece, string malformedPiece, string refusal)
    {
        // A function that loads as written, each case making one of its pieces malformed. The keyframe and Linear
        // pieces sit in the AddendCurve, which the function keeps as loaded, so a malformed piece let through there
        // shows as a function that loads. A MultiplierCurve is read again for negative keyframes, and a malformed piece
        // let through there throws in that read instead.
        const string wellFormed = """
                                  <WeightFunction Type="Curve" Rank="5">
                                      <AddendCurve Linear="false">
                                          <Keyframe Input="0" Value="0" InTangent="0" OutTangent="1"/>
                                          <Keyframe Input="10" Value="4"/>
                                      </AddendCurve>
                                      <MultiplierCurve>
                                          <Keyframe Input="0" Value="1"/>
                                      </MultiplierCurve>
                                  </WeightFunction>
                                  """;
        AiCreationData creationData =
            LoadingSpecHelpers.ParseForLoad(wellFormed.Replace(piece, malformedPiece), _logSink);

        // The factory refuses a stray child before Init runs, so each case loads through the factory.
        bool wasCreated =
            creationData.Factories.WeightFunctions.TryCreate(creationData, out WeightFunctionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries[0], Is.EqualTo($"Error guard.xml {refusal}"));
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
