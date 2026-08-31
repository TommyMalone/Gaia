using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="FactorySet" />, the built-in registrations its constructor installs.</summary>
[TestFixture]
public class FactorySetSpec
{
    [Test]
    public void BuildsEveryBuiltInWeightFunctionByItsTypeName()
    {
        (string typeName, Type componentType)[] catalog =
        {
            ("Constant", typeof(ConstantWeightFunction)),
            ("Boolean", typeof(BooleanWeightFunction)),
            ("String", typeof(StringWeightFunction)),
            ("FloatSequence", typeof(FloatSequenceWeightFunction)),
            ("Curve", typeof(CurveWeightFunction)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, Type componentType) in catalog)
            {
                string snippet = $"""<WeightFunction Type="{typeName}"/>""";
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                bool created = creationData.Factories.WeightFunctions.TryCreate(creationData,
                    out WeightFunctionBase? weightFunction);

                Assert.That(created, Is.True, typeName);
                Assert.That(weightFunction, Is.InstanceOf(componentType), typeName);
                Assert.That(logSink.Entries, Is.Empty, typeName);
            }
        });
    }
}
