using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>Covers <see cref="TuningConsideration" />, the fixed values it reports and how it loads them.</summary>
[TestFixture]
public class TuningConsiderationSpec
{
    [SetUp]
    public void BuildSink()
    {
        _logSink = new RecordingLogSink();
    }

    private RecordingLogSink _logSink;

    [Test]
    public void ReportsItsConfiguredValuesWithoutCalculating()
    {
        WeightValues configuredWeights = new(2f, 0.5f, 1f);

        TuningConsideration consideration = new(configuredWeights);

        consideration.Calculate();

        Assert.That(consideration.Weights, Is.EqualTo(configuredWeights));
    }

    [Test]
    public void LoadsItsValuesFromItsAttributes()
    {
        const string snippet = """
                               <Consideration Type="Tuning" Addend="1" Multiplier="0.5" Rank="2"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        TuningConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Weights, Is.EqualTo(new WeightValues(1f, 0.5f, 2f)));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesElementsItDoesNotTake()
    {
        const string snippet = """
                               <Consideration Type="Tuning">
                                   <Extra/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        TuningConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Consideration element does not take Extra elements",
            }));
        });
    }
}
