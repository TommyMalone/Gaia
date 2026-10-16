using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="BooleanWeightFunction" />.</summary>
[TestFixture]
public class BooleanWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _logSink = new RecordingLogSink();
        _trueWeights = new WeightValues(2f, 1f, WeightValues.NoRank);
        _falseWeights = WeightValues.Vetoed;

        _weightFunction = new BooleanWeightFunction(_trueWeights, _falseWeights);
    }

    private RecordingLogSink _logSink;

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

    [Test]
    public void LoadsEachAnswersValuesFromItsElement()
    {
        const string snippet = """
                               <WeightFunction Type="Boolean">
                                   <True Addend="2"/>
                                   <False Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        BooleanWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.CalculateBool(true), Is.EqualTo(new WeightValues(2f, 1f, WeightValues.NoRank)));
            Assert.That(loaded.CalculateBool(false), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LeavesAnUnmentionedAnswerNeutral()
    {
        const string snippet = """
                               <WeightFunction Type="Boolean">
                                   <False Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        BooleanWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.CalculateBool(true), Is.EqualTo(WeightValues.Neutral));
        });
    }

    [Test]
    public void RefusesElementsItDoesNotTake()
    {
        const string snippet = """
                               <WeightFunction Type="Boolean">
                                   <Maybe/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the child before Init runs, so the test loads through the factory rather than calling
        // Init.
        bool wasCreated =
            creationData.Factories.WeightFunctions.TryCreate(creationData, out WeightFunctionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the WeightFunction element does not take Maybe elements",
            }));
        });
    }
}
