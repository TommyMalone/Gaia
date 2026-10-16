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
    public void ContributesNothingBeforeAnythingSetsIt()
    {
        // The struct's own default would be a multiplier of 0 and a rank of 0, a veto that also claims a rank
        // above the identity, which is not what a function the host never configured should report.
        ConstantWeightFunction built = new();

        Assert.That(built.CalculateFloat(1f), Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void ReadsAVetoWrittenAsAnAttribute()
    {
        // Every weight element reads its values through the same method, so this covers the attribute for all
        // ten of them, and the constant is the one with nothing else in the way.
        const string snippet = """
                               <WeightFunction Type="Constant" Addend="5" Veto="true"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Weights, Is.EqualTo(new WeightValues(5f, 0f, WeightValues.NoRank)),
                "the veto is the multiplier, and the addend beside it is kept as written");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void KeepsItsDefaultsForAVetoWrittenFalse()
    {
        const string snippet = """
                               <WeightFunction Type="Constant" Veto="false"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Weights, Is.EqualTo(WeightValues.Neutral));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAVetoWrittenBesideAMultiplier()
    {
        // Both say what the multiplier is. An author who wrote both has two opinions and the reader picks
        // neither.
        const string snippet = """
                               <WeightFunction Type="Constant" Veto="true" Multiplier="0.5"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the WeightFunction element mixes Veto with Multiplier",
            }));
        });
    }

    [Test]
    public void RefusesAVetoOfFalseWrittenBesideAMultiplier()
    {
        // A Veto beside a Multiplier is refused whatever the Veto says. Veto="false" beside a Multiplier of 0 is what a
        // designer writes when removing a veto through the wrong attribute.
        const string snippet = """
                               <WeightFunction Type="Constant" Veto="false" Multiplier="0"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConstantWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the WeightFunction element mixes Veto with Multiplier",
            }));
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
                "Error guard.xml Line 2, the WeightFunction element does not take Extra elements",
            }));
        });
    }
}
