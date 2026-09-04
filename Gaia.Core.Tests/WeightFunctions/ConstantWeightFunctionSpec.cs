using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="ConstantWeightFunction" />.</summary>
[TestFixture]
public class ConstantWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _logSink = new RecordingLogSink();
        _configuredWeights = new WeightValues(3f, 0.5f, 2f);

        _weightFunction = new ConstantWeightFunction { Weights = _configuredWeights };
    }

    private RecordingLogSink _logSink;

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

    [Test]
    public void LoadsItsValuesFromItsAttributes()
    {
        const string snippet = """
                               <WeightFunction Type="Constant" Addend="3" Multiplier="0.5" Rank="2"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Weights, Is.EqualTo(new WeightValues(3f, 0.5f, 2f)));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LoadsNeutralValuesFromABareElement()
    {
        const string snippet = """
                               <WeightFunction Type="Constant"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Weights, Is.EqualTo(WeightValues.Neutral));
        });
    }

    [Test]
    public void RefusesANegativeMultiplier()
    {
        // Two multipliers of -1 would cancel to a positive weight and turn strongly discouraged into selected, so a
        // negative multiplier is refused where text becomes values.
        const string snippet = """
                               <WeightFunction Type="Constant" Multiplier="-1"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the WeightFunction element's Multiplier is not 0 or above, it reads \"-1\"",
            }));
        });
    }

    [Test]
    public void RefusesAMalformedAttribute()
    {
        const string snippet = """
                               <WeightFunction Type="Constant" Multiplier="high"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the WeightFunction element's Multiplier is not a number, it reads \"high\"",
            }));
        });
    }

    [Test]
    public void RefusesElementsItDoesNotTake()
    {
        const string snippet = """
                               <WeightFunction Type="Constant">
                                   <Extra/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the WeightFunction element does not take Extra elements",
            }));
        });
    }
}
