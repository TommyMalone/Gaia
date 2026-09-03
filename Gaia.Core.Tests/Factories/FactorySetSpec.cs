using Gaia.Core.Considerations;
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

    [Test]
    public void BuildsEveryBuiltInConsiderationByItsTypeName()
    {
        (string typeName, string snippet, Type componentType)[] catalog =
        {
            ("Tuning", """<Consideration Type="Tuning" Addend="1"/>""", typeof(TuningConsideration)),
            ("BooleanVariable", VariableSnippet("BooleanVariable"), typeof(BooleanVariableConsideration)),
            ("FloatVariable", VariableSnippet("FloatVariable"), typeof(FloatVariableConsideration)),
            ("IntVariable", VariableSnippet("IntVariable"), typeof(IntVariableConsideration)),
            ("StringVariable", VariableSnippet("StringVariable"), typeof(StringVariableConsideration)),
            ("EntityExists", VariableSnippet("EntityExists"), typeof(EntityExistsConsideration)),
            ("ExecutionHistory", """<Consideration Type="ExecutionHistory"/>""", typeof(ExecutionHistoryConsideration)),
            ("ConsiderationSet", """<Consideration Type="ConsiderationSet"/>""", typeof(ConsiderationSetConsideration)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, string snippet, Type componentType) in catalog)
            {
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                bool created = creationData.Factories.Considerations.TryCreate(creationData,
                    out ConsiderationBase? consideration);

                Assert.That(created, Is.True, typeName);
                Assert.That(consideration, Is.InstanceOf(componentType), typeName);
                Assert.That(logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    /// <summary>Builds the smallest loadable snippet for a variable consideration of the given type.</summary>
    private static string VariableSnippet(string typeName)
    {
        return $"""
                <Consideration Type="{typeName}" Variable="Anything">
                    <WeightFunction Type="Constant"/>
                </Consideration>
                """;
    }
}
