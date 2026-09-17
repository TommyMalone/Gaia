using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="FactorySet" />, the built-in registrations its constructor installs.</summary>
[TestFixture]
public class FactorySetSpec
{
    [SetUp]
    public void BuildSink()
    {
        _logSink = new RecordingLogSink();
    }

    private RecordingLogSink _logSink;

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
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.WeightFunctions.TryCreate(creationData,
                    out WeightFunctionBase? weightFunction);

                Assert.That(created, Is.True, typeName);
                Assert.That(weightFunction, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    [Test]
    public void BuildsEveryBuiltInConsiderationByItsTypeName()
    {
        (string typeName, string snippet, Type componentType)[] catalog =
        {
            ("Tuning", """<Consideration Type="Tuning" Addend="1"/>""", typeof(TuningConsideration)),
            ("Distance", DistanceSnippet(), typeof(DistanceConsideration)),
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
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.Considerations.TryCreate(creationData,
                    out ConsiderationBase? consideration);

                Assert.That(created, Is.True, typeName);
                Assert.That(consideration, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    [Test]
    public void BuildsEveryBuiltInTargetByItsTypeName()
    {
        (string typeName, string snippet, Type componentType)[] catalog =
        {
            ("Position", """<Target Type="Position" Position="(0, 0, 0)"/>""", typeof(PositionTarget)),
            ("Self", """<Target Type="Self"/>""", typeof(SelfTarget)),
            ("ByName", """<Target Type="ByName" List="Contacts" Entity="Officer"/>""", typeof(ByNameTarget)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, string snippet, Type componentType) in catalog)
            {
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.Targets.TryCreate(creationData, out TargetBase? target);

                Assert.That(created, Is.True, typeName);
                Assert.That(target, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    [Test]
    public void BuildsEveryBuiltInRegionByItsTypeName()
    {
        (string typeName, string snippet, Type componentType)[] catalog =
        {
            ("Circle", """<Region Type="Circle" Origin="(0, 0, 0)" Radius="5"/>""", typeof(CircleRegion)),
            ("Rectangle", """<Region Type="Rectangle" Min="(0, 0)" Max="(10, 10)"/>""", typeof(RectangleRegion)),
            ("Polygon", FootprintSnippet("Polygon", ""), typeof(PolygonRegion)),
            ("Sphere", """<Region Type="Sphere" Origin="(0, 0, 0)" Radius="5"/>""", typeof(SphereRegion)),
            ("Box", """<Region Type="Box" Min="(0, 0, 0)" Max="(10, 10, 10)"/>""", typeof(BoxRegion)),
            ("Cylinder", """<Region Type="Cylinder" Origin="(0, 0, 0)" Radius="5" HalfHeight="1.5"/>""",
                typeof(CylinderRegion)),
            ("Capsule", """<Region Type="Capsule" Origin="(0, 0, 0)" Radius="2" HalfHeight="3"/>""",
                typeof(CapsuleRegion)),
            ("ExtrudedPolygon", FootprintSnippet("ExtrudedPolygon", " HalfHeight=\"1.5\""),
                typeof(ExtrudedPolygonRegion)),
            ("ConvexHull", HullSnippet(), typeof(ConvexHullRegion)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, string snippet, Type componentType) in catalog)
            {
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? region);

                Assert.That(created, Is.True, typeName);
                Assert.That(region, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    [Test]
    public void BuildsEveryBuiltInActionByItsTypeName()
    {
        (string typeName, string snippet, Type componentType)[] catalog =
        {
            ("Pause", """<Action Type="Pause" Name="Wait" Duration="2"/>""", typeof(PauseAction)),
            ("Subreasoner", SubreasonerSnippet(), typeof(SubreasonerAction)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, string snippet, Type componentType) in catalog)
            {
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.Actions.TryCreate(creationData, out ActionBase? action);

                Assert.That(created, Is.True, typeName);
                Assert.That(action, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    [Test]
    public void BuildsTheBuiltInOptionByItsTypeName()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Idle"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool created = creationData.Factories.Options.TryCreate(creationData, out OptionBase? option);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(option, Is.InstanceOf<ConsiderationAndActionOption>());
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void BuildsEveryBuiltInReasonerByItsTypeName()
    {
        (string typeName, Type componentType)[] catalog =
        {
            ("RuleBased", typeof(RuleBasedReasoner)),
            ("DualUtility", typeof(DualUtilityReasoner)),
            ("Sequence", typeof(SequenceReasoner)),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, Type componentType) in catalog)
            {
                string snippet = $"""<Reasoner Type="{typeName}"/>""";
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

                bool created = creationData.Factories.Reasoners.TryCreate(creationData, out ReasonerBase? reasoner);

                Assert.That(created, Is.True, typeName);
                Assert.That(reasoner, Is.InstanceOf(componentType), typeName);
                Assert.That(_logSink.Entries, Is.Empty, typeName);
            }
        });
    }

    /// <summary>Builds the smallest loadable snippet for a Subreasoner action.</summary>
    private static string SubreasonerSnippet()
    {
        return """
               <Action Type="Subreasoner" Name="Investigate">
                   <Reasoner Type="Sequence"/>
               </Action>
               """;
    }

    /// <summary>Builds the smallest loadable snippet for a Distance consideration.</summary>
    private static string DistanceSnippet()
    {
        return """
               <Consideration Type="Distance">
                   <From Type="Position" Position="(0, 0, 0)"/>
                   <To Type="Position" Position="(0, 0, 10)"/>
                   <WeightFunction Type="Constant"/>
               </Consideration>
               """;
    }

    /// <summary>Builds the smallest loadable snippet for a footprint region of the given type.</summary>
    private static string FootprintSnippet(string typeName, string extraAttributes)
    {
        return $"""
                <Region Type="{typeName}"{extraAttributes}>
                    <Corner Position="(0, 0)"/>
                    <Corner Position="(10, 0)"/>
                    <Corner Position="(0, 10)"/>
                </Region>
                """;
    }

    /// <summary>Builds the smallest loadable snippet for a convex hull, a unit box of six planes.</summary>
    private static string HullSnippet()
    {
        return """
               <Region Type="ConvexHull">
                   <Plane Normal="(1, 0, 0)" Distance="1"/>
                   <Plane Normal="(-1, 0, 0)" Distance="1"/>
                   <Plane Normal="(0, 1, 0)" Distance="1"/>
                   <Plane Normal="(0, -1, 0)" Distance="1"/>
                   <Plane Normal="(0, 0, 1)" Distance="1"/>
                   <Plane Normal="(0, 0, -1)" Distance="1"/>
               </Region>
               """;
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
