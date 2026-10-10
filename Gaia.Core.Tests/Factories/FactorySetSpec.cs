using System.Reflection;
using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Strings;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers <see cref="FactorySet" />, the built-in registrations its constructor installs, the order and spelling
///     it lists them in, the root's name, and what every registered type refuses through its description.
/// </summary>
/// <remarks>
///     The sweeps over every registered type walk <see cref="ConceptualAbstraction" /> through Enum.GetValues, so a
///     factory added later is swept without this spec naming it. Each sweep builds the element through its abstraction's
///     factory, since the factory is what runs the description check.
/// </remarks>
[TestFixture]
public class FactorySetSpec
{
    [SetUp]
    public void BuildSink()
    {
        _logSink = new RecordingLogSink();
    }

    /// <summary>How many types the constructor registers, which the sweeps over every type hold themselves to.</summary>
    private const int BuiltInTypeCount = 32;

    /// <summary>The element name a sweep writes for each abstraction, which is the name the reports print.</summary>
    private static readonly Dictionary<ConceptualAbstraction, string> ElementNames = new()
    {
        [ConceptualAbstraction.Actions] = "Action",
        [ConceptualAbstraction.Considerations] = "Consideration",
        [ConceptualAbstraction.Options] = "Option",
        [ConceptualAbstraction.Reasoners] = "Reasoner",
        [ConceptualAbstraction.Regions] = "Region",
        [ConceptualAbstraction.Targets] = "Target",
        [ConceptualAbstraction.WeightFunctions] = "WeightFunction",
    };

    /// <summary>The TryCreate of each abstraction's factory, since the generic factories share no building method.</summary>
    private static readonly Dictionary<ConceptualAbstraction, Func<AiCreationData, bool>> Creators = new()
    {
        [ConceptualAbstraction.Actions] = creationData => creationData.Factories.Actions.TryCreate(creationData, out _),
        [ConceptualAbstraction.Considerations] = creationData =>
            creationData.Factories.Considerations.TryCreate(creationData, out _),
        [ConceptualAbstraction.Options] = creationData => creationData.Factories.Options.TryCreate(creationData, out _),
        [ConceptualAbstraction.Reasoners] = creationData =>
            creationData.Factories.Reasoners.TryCreate(creationData, out _),
        [ConceptualAbstraction.Regions] = creationData => creationData.Factories.Regions.TryCreate(creationData, out _),
        [ConceptualAbstraction.Targets] = creationData => creationData.Factories.Targets.TryCreate(creationData, out _),
        [ConceptualAbstraction.WeightFunctions] = creationData =>
            creationData.Factories.WeightFunctions.TryCreate(creationData, out _),
    };

    private RecordingLogSink _logSink;

    [Test]
    public void EnumeratesEachAbstractionInRegistrationOrder()
    {
        FactorySet factories = new();

        Assert.Multiple(() =>
        {
            Assert.That(factories.Actions.Types, Is.EqualTo(Names("Pause", "Subreasoner")));
            Assert.That(factories.Considerations.Types,
                Is.EqualTo(Names("BooleanVariable", "ConsiderationSet", "Distance", "EntityExists", "ExecutionHistory",
                    "FloatVariable", "IntVariable", "StringVariable", "Tuning")));
            Assert.That(factories.Options.Types, Is.EqualTo(Names("ConsiderationAndAction")));
            Assert.That(factories.Reasoners.Types, Is.EqualTo(Names("DualUtility", "RuleBased", "Sequence")));
            Assert.That(factories.Regions.Types,
                Is.EqualTo(Names("Box", "Capsule", "Circle", "ConvexHull", "Cylinder", "ExtrudedPolygon", "Polygon",
                    "Rectangle", "Sphere")));
            Assert.That(factories.Targets.Types, Is.EqualTo(Names("ByName", "Position", "Self")));
            Assert.That(factories.WeightFunctions.Types,
                Is.EqualTo(Names("Constant", "Boolean", "String", "FloatSequence", "Curve")));
        });
    }

    [Test]
    public void SpellsEveryBuiltInTypeAsItsRegistrationDoes()
    {
        FactorySet factories = new();

        Assert.Multiple(() =>
        {
            Assert.That(factories.Actions.TypeSpellings, Is.EqualTo(new[] { "Pause", "Subreasoner" }));
            Assert.That(factories.Considerations.TypeSpellings, Is.EqualTo(new[]
            {
                "BooleanVariable", "ConsiderationSet", "Distance", "EntityExists", "ExecutionHistory", "FloatVariable",
                "IntVariable", "StringVariable", "Tuning",
            }));
            Assert.That(factories.Options.TypeSpellings, Is.EqualTo(new[] { "ConsiderationAndAction" }));
            Assert.That(factories.Reasoners.TypeSpellings,
                Is.EqualTo(new[] { "DualUtility", "RuleBased", "Sequence" }));
            Assert.That(factories.Regions.TypeSpellings, Is.EqualTo(new[]
            {
                "Box", "Capsule", "Circle", "ConvexHull", "Cylinder", "ExtrudedPolygon", "Polygon", "Rectangle",
                "Sphere",
            }));
            Assert.That(factories.Targets.TypeSpellings, Is.EqualTo(new[] { "ByName", "Position", "Self" }));
            Assert.That(factories.WeightFunctions.TypeSpellings,
                Is.EqualTo(new[] { "Constant", "Boolean", "String", "FloatSequence", "Curve" }));
        });
    }

    [Test]
    public void AnswersTheFactoryOfEachConceptualAbstraction()
    {
        FactorySet factories = new();

        Assert.Multiple(() =>
        {
            foreach (ConceptualAbstraction abstraction in Enum.GetValues<ConceptualAbstraction>())
            {
                // Each value is named for the property that holds its factory, so the property is looked up by the
                // value's name and an abstraction added later is answered or fails here.
                PropertyInfo? property = typeof(FactorySet).GetProperty(abstraction.ToString());

                Assert.That(property, Is.Not.Null, $"{abstraction} names no property of the set");
                Assert.That(factories.Factory(abstraction), Is.SameAs(property!.GetValue(factories)),
                    $"{abstraction} is answered with another factory");
            }
        });
    }

    [Test]
    public void ThrowsForAValueNoConceptualAbstractionNames()
    {
        FactorySet factories = new();

        Assert.That(() => factories.Factory((ConceptualAbstraction)7), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void NamesTheRootElementBrain()
    {
        Assert.That(FactorySet.RootName, Is.EqualTo("Brain"));
    }

    [Test]
    public void DescribesTheRootAsABrainHoldingOneReasonerAtMost()
    {
        FactorySet factories = new();

        ComponentDescription root = factories.Root;
        SlotDescription reasoner = root.Slots[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.Abstraction, Is.Null, "no factory builds a brain");
            Assert.That(root.Attributes, Is.Empty);
            Assert.That(root.Slots, Has.Count.EqualTo(1));
            Assert.That(reasoner.Spelling, Is.EqualTo("Reasoner"));
            Assert.That(reasoner.Abstraction, Is.EqualTo(ConceptualAbstraction.Reasoners));
            Assert.That(reasoner.MinCount, Is.EqualTo(0), "a Brain with no Reasoner loads");
            Assert.That(reasoner.MaxCount, Is.EqualTo(1), "a second Reasoner is refused");
        });
    }

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
            ("Rectangle", """<Region Type="Rectangle" Origin="(5, 0, 5)" Extents="(5, 5)"/>""",
                typeof(RectangleRegion)),
            ("Polygon", FootprintSnippet("Polygon", ""), typeof(PolygonRegion)),
            ("Sphere", """<Region Type="Sphere" Origin="(0, 0, 0)" Radius="5"/>""", typeof(SphereRegion)),
            ("Box", """<Region Type="Box" Origin="(5, 5, 5)" Extents="(5, 5, 5)"/>""", typeof(BoxRegion)),
            ("Cylinder", """<Region Type="Cylinder" Origin="(0, 0, 0)" Radius="5" HalfHeight="1.5"/>""",
                typeof(CylinderRegion)),
            ("Capsule", """<Region Type="Capsule" Origin="(0, 0, 0)" Radius="2" HalfHeight="5"/>""",
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

    [Test]
    public void RefusesAnUndescribedAttributeOnEveryRegisteredType()
    {
        // The check runs before Init, so the snippet writes none of the attributes a type requires and is still
        // refused for the one it does not describe.
        int swept = 0;

        Assert.Multiple(() =>
        {
            foreach ((ConceptualAbstraction abstraction, string typeName) in EveryRegisteredType())
            {
                string elementName = ElementNames[abstraction];
                string snippet = $"""<{elementName} Type="{typeName}" Ranck="1"/>""";
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                Assert.That(Creators[abstraction](creationData), Is.False, typeName);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                {
                    $"Error guard.xml Line 1, the {elementName} element does not take the Ranck attribute",
                }), typeName);
                swept++;
            }

            Assert.That(swept, Is.EqualTo(BuiltInTypeCount));
        });
    }

    [Test]
    public void RefusesAJunkChildInEveryRegisteredType()
    {
        int swept = 0;

        Assert.Multiple(() =>
        {
            foreach ((ConceptualAbstraction abstraction, string typeName) in EveryRegisteredType())
            {
                string elementName = ElementNames[abstraction];
                string snippet = $"""
                                  <{elementName} Type="{typeName}">
                                      <Junk/>
                                  </{elementName}>
                                  """;
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                Assert.That(Creators[abstraction](creationData), Is.False, typeName);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                {
                    $"Error guard.xml Line 2, the {elementName} element does not take Junk elements",
                }), typeName);
                swept++;
            }

            Assert.That(swept, Is.EqualTo(BuiltInTypeCount));
        });
    }

    [Test]
    public void RefusesASecondChildInEverySlotThatTakesOne()
    {
        // The check refuses the second child before any child is built, so a bare child element with no Type is
        // enough to fill a slot of an abstraction as well as an inline one.
        FactorySet factories = new();
        int swept = 0;

        Assert.Multiple(() =>
        {
            foreach ((ConceptualAbstraction abstraction, string typeName) in EveryRegisteredType())
            {
                factories.Factory(abstraction).TryGetDescription(typeName, out ComponentDescription? description);

                foreach (SlotDescription slot in description!.Slots)
                {
                    if (slot.MaxCount != 1)
                    {
                        continue;
                    }

                    string elementName = ElementNames[abstraction];
                    string snippet = $"""
                                      <{elementName} Type="{typeName}">
                                          <{slot.Spelling}/>
                                          <{slot.Spelling}/>
                                      </{elementName}>
                                      """;
                    RecordingLogSink logSink = new();
                    AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);
                    string subject = $"{typeName} {slot.Spelling}";

                    Assert.That(Creators[abstraction](creationData), Is.False, subject);
                    Assert.That(logSink.Entries, Is.EqualTo(new[]
                    {
                        $"Error guard.xml Line 3, the {elementName} element takes only one {slot.Spelling} element",
                    }), subject);
                    swept++;
                }
            }

            Assert.That(swept, Is.GreaterThan(0), "the shipped descriptions carry slots that take one child");
        });
    }

    [Test]
    public void RefusesAMissingRequiredAttributeInEveryRegion()
    {
        // Origin is optional on every shape that takes one, so a bare round shape names Radius. The capsule and the
        // cylinder name HalfHeight once given a Radius, the extruded polygon names HalfHeight, and the box and the
        // rectangle name Extents.
        AssertMissingAttributeRefused("Region", new[]
        {
            ("Box", "", "Extents"), ("Capsule", "", "Radius"), ("Capsule", " Radius=\"2\"", "HalfHeight"),
            ("Circle", "", "Radius"), ("Cylinder", "", "Radius"), ("Cylinder", " Radius=\"5\"", "HalfHeight"),
            ("ExtrudedPolygon", "", "HalfHeight"), ("Rectangle", "", "Extents"), ("Sphere", "", "Radius"),
        }, creationData => creationData.Factories.Regions.TryCreate(creationData, out _));
    }

    [Test]
    public void RefusesAMissingRequiredAttributeInTargetsActionsAndTheOption()
    {
        AssertMissingAttributeRefused("Target",
            new[] { ("Position", "", "Position"), ("ByName", " List=\"Contacts\"", "Entity") },
            creationData => creationData.Factories.Targets.TryCreate(creationData, out _));

        AssertMissingAttributeRefused("Action",
            new[] { ("Pause", "", "Name"), ("Pause", " Name=\"Wait\"", "Duration") },
            creationData => creationData.Factories.Actions.TryCreate(creationData, out _));

        AssertMissingAttributeRefused("Option", new[] { ("ConsiderationAndAction", "", "Name") },
            creationData => creationData.Factories.Options.TryCreate(creationData, out _));
    }

    /// <summary>Every registered type of every abstraction of a fresh set, as the abstraction and the type's spelling.</summary>
    private static IEnumerable<(ConceptualAbstraction abstraction, string typeName)> EveryRegisteredType()
    {
        FactorySet factories = new();

        foreach (ConceptualAbstraction abstraction in Enum.GetValues<ConceptualAbstraction>())
        {
            foreach (string typeName in factories.Factory(abstraction).TypeSpellings)
            {
                yield return (abstraction, typeName);
            }
        }
    }

    /// <summary>Asserts that each catalog entry reports the first required attribute its snippet leaves out.</summary>
    private static void AssertMissingAttributeRefused(
        string elementName,
        (string typeName, string extraAttributes, string missingAttribute)[] catalog,
        Func<AiCreationData, bool> create)
    {
        Assert.Multiple(() =>
        {
            foreach ((string typeName, string extraAttributes, string missingAttribute) in catalog)
            {
                string snippet = $"""<{elementName} Type="{typeName}"{extraAttributes}/>""";
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                Assert.That(create(creationData), Is.False, typeName);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                    {
                        $"Error guard.xml Line 1, the {elementName} element has no {missingAttribute} attribute",
                        $"Error guard.xml Line 1, the {elementName} element's type {typeName} failed to initialize",
                    }), $"{typeName} {missingAttribute}");
            }
        });
    }

    /// <summary>
    ///     The given texts as the names a factory lists, so an expected order compares by identity rather than by
    ///     spelling.
    /// </summary>
    private static AiString[] Names(params string[] texts)
    {
        AiString[] names = new AiString[texts.Length];

        for (int index = 0; index < texts.Length; index++)
        {
            names[index] = texts[index];
        }

        return names;
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
