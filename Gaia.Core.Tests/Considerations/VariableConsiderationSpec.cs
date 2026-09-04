using Gaia.Core.Blackboards;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>Covers what separates the four variable considerations, the type each reads and the conversion it reaches.</summary>
/// <remarks>
///     The weight function is stubbed, so what is asserted is which conversion was reached and what value got there,
///     rather than anything about the conversion itself. The store is a <see cref="BrainBlackboard" /> because any store
///     would serve and nothing here depends on which kind it is.
/// </remarks>
[TestFixture]
public class VariableConsiderationSpec
{
    [SetUp]
    public void BuildConsiderations()
    {
        _logSink = new RecordingLogSink();
        _blackboard = new BrainBlackboard();

        _reportedWeights = new WeightValues(3f, 0.5f, 2f);
        _missingWeights = WeightValues.Vetoed;

        _weightFunction = new RecordingWeightFunction { Result = _reportedWeights };
    }

    private RecordingLogSink _logSink;

    private BrainBlackboard _blackboard;

    private WeightValues _reportedWeights;
    private WeightValues _missingWeights;

    private RecordingWeightFunction _weightFunction;

    [Test]
    public void ReadsABoolAndHandsItAlong()
    {
        _blackboard.SetBool("IsFriendly", true);

        BooleanVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "IsFriendly",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastBool, Is.True);
            Assert.That(consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void ReadsAnIntAndHandsItAlong()
    {
        _blackboard.SetInt("Hunger", 7);

        IntVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Hunger",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastInt, Is.EqualTo(7));
            Assert.That(consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void ReadsAFloatAndHandsItAlong()
    {
        _blackboard.SetFloat("Health", 0.25f);

        FloatVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Health",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastFloat, Is.EqualTo(0.25f));
            Assert.That(consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void ReadsAStringAndHandsItAlong()
    {
        _blackboard.SetString("Side", "Enemy");

        StringVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Side",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastString.Text, Is.EqualTo("Enemy"));
            Assert.That(consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void ReportsItsMissingValuesWhenTheStoreHasNothingUnderThatName()
    {
        FloatVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Health",
            WeightFunction = _weightFunction,
            MissingVariableWeights = _missingWeights,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(consideration.Weights, Is.EqualTo(_missingWeights));

            // There is nothing to convert, so the weight function is never asked.
            Assert.That(_weightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReportsItsMissingValuesWithNoStoreToReadFrom()
    {
        FloatVariableConsideration consideration = new()
        {
            VariableName = "Health",
            WeightFunction = _weightFunction,
            MissingVariableWeights = _missingWeights,
        };

        consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(consideration.Weights, Is.EqualTo(_missingWeights));
            Assert.That(_weightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReportsNeutralValuesForAMissingVariableByDefault()
    {
        // A variable that has not been written yet leaves its option alone rather than vetoing it.
        FloatVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Health",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.That(consideration.Weights, Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void ReadsTheVariableAgainEveryCycle()
    {
        _blackboard.SetFloat("Health", 1f);

        FloatVariableConsideration consideration = new()
        {
            DataStore = _blackboard,
            VariableName = "Health",
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(1f));

        _blackboard.SetFloat("Health", 0.25f);

        consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(0.25f));
    }

    [Test]
    public void LoadsItsConfigurationFromANode()
    {
        const string snippet = """
                               <Consideration Type="FloatVariable" Variable="Health">
                                   <Missing Multiplier="0"/>
                                   <WeightFunction Type="Constant" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.BrainBlackboard = _blackboard;

        AiString healthName = "Health";
        FloatVariableConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.DataStore, Is.SameAs(_blackboard), "the brain store is the default");
            Assert.That(loaded.VariableName, Is.EqualTo(healthName));
            Assert.That(loaded.MissingVariableWeights, Is.EqualTo(WeightValues.Vetoed));
            Assert.That(loaded.WeightFunction, Is.InstanceOf<ConstantWeightFunction>());
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ReadsTheWorldStoreWhenAsked()
    {
        const string snippet = """
                               <Consideration Type="BooleanVariable" Variable="IsRaining" Store="World">
                                   <WeightFunction Type="Constant"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        WorldBlackboard worldBlackboard = new();
        creationData.BrainBlackboard = _blackboard;
        creationData.WorldBlackboard = worldBlackboard;

        BooleanVariableConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.DataStore, Is.SameAs(worldBlackboard));
        });
    }

    [Test]
    public void RefusesAStoreItDoesNotKnow()
    {
        const string snippet = """
                               <Consideration Type="BooleanVariable" Variable="IsRaining" Store="Global"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        BooleanVariableConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Consideration element's Store is not Brain or World, it reads \"Global\"",
            }));
        });
    }

    [Test]
    public void RefusesANodeWithNoWeightFunction()
    {
        const string snippet = """
                               <Consideration Type="FloatVariable" Variable="Health"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatVariableConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Consideration element has no WeightFunction element",
            }));
        });
    }

    [Test]
    public void RefusesTheWorldStoreWhenNoneWasSupplied()
    {
        // The loader knows at load time that no world blackboard exists, so a configuration that asks for one is
        // refused there rather than silently reading missing values forever.
        const string snippet = """
                               <Consideration Type="BooleanVariable" Variable="IsRaining" Store="World">
                                   <WeightFunction Type="Boolean"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        BooleanVariableConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Consideration element's Store is World and no world blackboard was supplied",
            }));
        });
    }
}
