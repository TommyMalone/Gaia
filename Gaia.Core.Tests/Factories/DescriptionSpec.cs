using Gaia.Core.Actions;
using Gaia.Core.Blackboards;
using Gaia.Core.Considerations;
using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers the component descriptions. Every registered type has one, the shipped configurations carry nothing a
///     description does not name, a sample of the names a base reads on a component's behalf is described, and the tests
///     besides these assert what a description says about a name, its kind, its choices, its default, the count a slot
///     takes and what fills it.
/// </summary>
/// <remarks>
///     A name a component reads for itself is an <see cref="AttributeDescription" /> or
///     <see cref="SlotDescription" /> field made through the component's own <see cref="DescriptionBuilder" />, which
///     records each field as it hands it back, or one of the fields WeightValues makes and lists, which
///     TuningConsideration and ConstantWeightFunction read through WeightValues.TryRead, so that name is in the
///     description without a test asserting it. A name a base reads on the component's behalf is in the description
///     because the component started from the builder the base hands out or wrote an Add, so a test asserts it for a
///     sample of them, every action's Name, every reasoner's Option, every variable consideration's Store, and the region
///     base's attributes and target slot and the footprint base's Corner slot on the polygon. Every other test, apart from
///     the two walks over the registered types and the shipped configurations, asserts what a description says about a
///     name, the order of the attributes, the kind an attribute is read as, its choices and its default, and the count a
///     slot takes and what fills it.
/// </remarks>
[TestFixture]
public class DescriptionSpec
{
    [SetUp]
    public void BuildSet()
    {
        _factories = new FactorySet();
    }

    /// <summary>
    ///     Every conceptual abstraction, read from the enumeration itself, so that a walk reaches every factory on the
    ///     set and an abstraction added later is walked without this spec naming it.
    /// </summary>
    private static readonly ConceptualAbstraction[] Abstractions = Enum.GetValues<ConceptualAbstraction>();

    private FactorySet _factories;

    [Test]
    public void DescribesEveryTypeItsFactoriesRegister()
    {
        Assert.Multiple(() =>
        {
            foreach (ConceptualAbstraction abstraction in Abstractions)
            {
                IComponentFactory factory = _factories.Factory(abstraction);

                foreach (AiString type in factory.Types)
                {
                    string subject = $"{abstraction} {type.Text}";

                    Assert.That(factory.TryGetDescription(type, out ComponentDescription? description), Is.True,
                        $"{subject} is registered with no description");
                    Assert.That(description!.Abstraction, Is.EqualTo(abstraction),
                        $"{subject} is described under another abstraction");
                    Assert.That(description.Summary, Is.Not.Empty, $"{subject} is described with no summary");
                }
            }
        });
    }

    [Test]
    public void DescribesTheNameEveryActionReadsAndTheOptionsEveryReasonerReads()
    {
        Assert.Multiple(() =>
        {
            foreach (AiString type in _factories.Actions.Types)
            {
                Assert.That(AttributeOf(DescriptionOf(ConceptualAbstraction.Actions, type)!, "Name"), Is.Not.Null,
                    $"the {type.Text} action does not describe the Name its base reads");
            }

            foreach (AiString type in _factories.Reasoners.Types)
            {
                Assert.That(SlotOf(DescriptionOf(ConceptualAbstraction.Reasoners, type)!, "Option"), Is.Not.Null,
                    $"the {type.Text} reasoner does not describe the Option children its base reads");
            }
        });
    }

    [TestCase("guard.xml")]
    [TestCase("sniper.xml")]
    public void NamesEveryAttributeTheShippedConfigurationsCarry(string fileName)
    {
        _factories.Actions.Register("Record", () => new RecordAction(new List<string>()), RecordAction.Description);

        RecordingLogSink logSink = new();
        XmlSpecificationReader reader = new(logSink);
        string configurationText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integration", fileName));

        Assert.That(reader.TryRead(configurationText, fileName, out SpecificationNode? root), Is.True);

        Assert.Multiple(() => AssertEveryAttributeIsDescribed(root!, _factories.Root));
    }

    [Test]
    public void ListsAComponentsAttributesInTheOrderItDeclaredThem()
    {
        ComponentDescription sphere = DescriptionOf(ConceptualAbstraction.Regions, "Sphere")!;

        Assert.That(sphere.Attributes.Select(attribute => attribute.Spelling),
            Is.EqualTo(new[] { "Origin", "Rotation", "Scale", "AbsoluteRotation", "AbsoluteScale", "Radius" }));
    }

    [Test]
    public void CarriesWhatARegionsBasesReadWithoutTheShapeNamingIt()
    {
        ComponentDescription polygon = DescriptionOf(ConceptualAbstraction.Regions, "Polygon")!;

        Assert.Multiple(() =>
        {
            Assert.That(AttributeOf(polygon, "Origin"), Is.Not.Null, "the region base reads Origin");
            Assert.That(AttributeOf(polygon, "Rotation"), Is.Not.Null, "the region base reads Rotation");
            Assert.That(AttributeOf(polygon, "Scale"), Is.Not.Null, "the region base reads Scale");
            Assert.That(AttributeOf(polygon, "AbsoluteRotation"), Is.Not.Null,
                "the region base reads AbsoluteRotation");
            Assert.That(AttributeOf(polygon, "AbsoluteScale"), Is.Not.Null, "the region base reads AbsoluteScale");
            Assert.That(polygon.Attributes, Has.Count.EqualTo(5), "a polygon reads no attribute of its own");
            Assert.That(polygon.Slots.Select(slot => slot.Spelling), Is.EqualTo(new[] { "OriginTarget", "Corner" }),
                "the region base reads the OriginTarget child and the footprint base the Corner children");
        });
    }

    [Test]
    public void DescribesASlotByTheAbstractionThatFillsIt()
    {
        SlotDescription consideration = SlotOf(DescriptionOf(ConceptualAbstraction.Options, "ConsiderationAndAction")!,
            "Consideration")!;

        Assert.Multiple(() =>
        {
            Assert.That(consideration.Abstraction, Is.EqualTo(ConceptualAbstraction.Considerations));
            Assert.That(consideration.Inline, Is.Null);
            Assert.That(consideration.MinCount, Is.EqualTo(0));
            Assert.That(consideration.MaxCount, Is.EqualTo(SlotDescription.Unbounded));
        });
    }

    [Test]
    public void DescribesAStructuralSlotInline()
    {
        ComponentDescription boolean = DescriptionOf(ConceptualAbstraction.WeightFunctions, "Boolean")!;
        SlotDescription trueSlot = SlotOf(boolean, "True")!;
        SlotDescription falseSlot = SlotOf(boolean, "False")!;

        Assert.Multiple(() =>
        {
            Assert.That(trueSlot.Abstraction, Is.Null);
            Assert.That(trueSlot.Inline, Is.SameAs(WeightValues.Description));
            Assert.That(falseSlot.Inline, Is.SameAs(WeightValues.Description));
            Assert.That(trueSlot.MaxCount, Is.EqualTo(1));
            Assert.That(falseSlot.MaxCount, Is.EqualTo(1));
            Assert.That(AttributeOf(trueSlot.Inline!, "Veto"), Is.Not.Null,
                "the inline element takes the Veto guard.xml writes");
        });
    }

    [Test]
    public void ListsAnEnumerationsChoicesFromItsType()
    {
        AttributeDescription doneWhen =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Actions, "Subreasoner")!, "DoneWhen")!;

        Assert.Multiple(() =>
        {
            Assert.That(doneWhen.Kind, Is.EqualTo(AttributeKind.Enumeration));
            Assert.That(doneWhen.Choices, Is.EqualTo(Enum.GetNames(typeof(SubreasonerCompletion))));
        });
    }

    [Test]
    public void ListsTheGroupsBooleanOperatorsWithTheirDefault()
    {
        AttributeDescription booleanOperator =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "Group")!, "BooleanOperator")!;

        Assert.Multiple(() =>
        {
            Assert.That(booleanOperator.Kind, Is.EqualTo(AttributeKind.Enumeration));
            Assert.That(booleanOperator.Choices, Is.EqualTo(new[] { "And", "Or", "Not" }));
            Assert.That(booleanOperator.DefaultText, Is.EqualTo("And"));
        });
    }

    [Test]
    public void ListsTheGroupsRankOperatorsWithTheirDefault()
    {
        AttributeDescription rankOperator =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "Group")!, "RankOperator")!;

        Assert.Multiple(() =>
        {
            Assert.That(rankOperator.Kind, Is.EqualTo(AttributeKind.Enumeration));
            Assert.That(rankOperator.Choices, Is.EqualTo(new[] { "Max", "Min", "Sum" }));
            Assert.That(rankOperator.DefaultText, Is.EqualTo("Max"));
        });
    }

    [Test]
    public void CarriesTheKindAVariableIsReadAs()
    {
        AttributeDescription floatVariable =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "FloatVariable")!, "Variable")!;
        AttributeDescription entityList =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "EntityExists")!, "Variable")!;
        AttributeDescription byNameList = AttributeOf(DescriptionOf(ConceptualAbstraction.Targets, "ByName")!, "List")!;

        Assert.Multiple(() =>
        {
            Assert.That(floatVariable.Kind, Is.EqualTo(AttributeKind.VariableName));
            Assert.That(floatVariable.VariableKind, Is.EqualTo(DataStoreValueType.Float));
            Assert.That(entityList.VariableKind, Is.EqualTo(DataStoreValueType.EntityList));
            Assert.That(byNameList.Kind, Is.EqualTo(AttributeKind.VariableName),
                "the list a ByName target searches is a stored name, as the list EntityExists reads is");
            Assert.That(byNameList.VariableKind, Is.EqualTo(DataStoreValueType.EntityList));
        });
    }

    [Test]
    public void ListsBrainAndWorldAsTheStoreChoices()
    {
        AttributeDescription store = AttributeOf(DescriptionOf(ConceptualAbstraction.Targets, "ByName")!, "Store")!;

        Assert.Multiple(() =>
        {
            Assert.That(store.Kind, Is.EqualTo(AttributeKind.Enumeration));
            Assert.That(store.Choices, Is.EqualTo(new[] { "Brain", "World" }));
            Assert.That(store.DefaultText, Is.EqualTo("Brain"));
        });
    }

    [Test]
    public void DescribesTheStoreEveryVariableConsiderationReads()
    {
        // Every variable consideration reads Store through its shared base, which also describes it, so a designer is
        // told of the attribute whichever of the five they write.
        string[] variableConsiderations =
            { "BooleanVariable", "EntityExists", "FloatVariable", "IntVariable", "StringVariable" };

        Assert.Multiple(() =>
        {
            foreach (string typeName in variableConsiderations)
            {
                AttributeDescription? store =
                    AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, typeName)!, "Store");

                Assert.That(store, Is.Not.Null, typeName);
                Assert.That(store?.Kind, Is.EqualTo(AttributeKind.Enumeration), typeName);
                Assert.That(store?.DefaultText, Is.EqualTo("Brain"), typeName);
            }
        });
    }

    [Test]
    public void GivesEveryDefaultAsTextTheReaderAccepts()
    {
        List<AttributeDescription> attributes = new();

        foreach (ConceptualAbstraction abstraction in Abstractions)
        {
            IComponentFactory factory = _factories.Factory(abstraction);

            foreach (AiString type in factory.Types)
            {
                CollectAttributes(DescriptionOf(abstraction, type)!, attributes);
            }
        }

        CollectAttributes(_factories.Root, attributes);

        int defaults = 0;

        Assert.Multiple(() =>
        {
            foreach (AttributeDescription attribute in attributes)
            {
                if (attribute.DefaultText == null)
                {
                    continue;
                }

                defaults++;
                Assert.That(ReadsAsItsKind(attribute), Is.True,
                    $"the {attribute.Spelling} default \"{attribute.DefaultText}\" does not read as {attribute.Kind}");
            }

            Assert.That(defaults, Is.GreaterThan(0), "the shipped descriptions carry defaults");
        });
    }

    [Test]
    public void AssignsTheDefaultItDescribesWhenTheAttributeIsAbsent()
    {
        ConstantWeightFunction constant = new();
        TuningConsideration tuning = new();
        ExecutionHistoryConsideration executionHistory = new();
        DualUtilityReasoner dualUtility = new();
        SubreasonerAction subreasoner = new();
        CurveWeightFunction curve = new();
        SphereRegion sphere = new();
        CapsuleRegion capsule = new();
        CircleRegion circle = new();
        ByNameTarget byName = new();
        DistanceConsideration distance = new();
        FloatSequenceWeightFunction floatSequence = new();
        GroupConsideration group = new();

        // Both blackboards are supplied, so the store a component loaded can be matched back to the Store name that
        // picked it.
        BrainBlackboard brainBlackboard = new();
        WorldBlackboard worldBlackboard = new();

        // The slope of the CurveSnippet's one segment, which Linear="true" would give the first keyframe's tangent.
        const float linearSlope = 0.4f;

        Assert.Multiple(() =>
        {
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Constant", "Addend", constant,
                "<WeightFunction Type=\"Constant\"/>", loaded => loaded.Weights.Addend);
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Constant", "Multiplier", constant,
                "<WeightFunction Type=\"Constant\"/>", loaded => loaded.Weights.Multiplier);
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "Tuning", "Addend", tuning,
                "<Consideration Type=\"Tuning\"/>", loaded => loaded.Weights.Addend);
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "Tuning", "Multiplier", tuning,
                "<Consideration Type=\"Tuning\"/>", loaded => loaded.Weights.Multiplier);
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "ExecutionHistory", "RankRetentionFactor",
                executionHistory, "<Consideration Type=\"ExecutionHistory\"/>", loaded => loaded.RankRetentionFactor);
            AssertAssignsItsDefault(ConceptualAbstraction.Reasoners, "DualUtility", "WeightScreeningFactor",
                dualUtility, "<Reasoner Type=\"DualUtility\"/>", loaded => loaded.WeightScreeningFactor);
            AssertAssignsItsDefault(ConceptualAbstraction.Actions, "Subreasoner", "NothingEligibleCycleLimit",
                subreasoner, "<Action Type=\"Subreasoner\" Name=\"Nest\"><Reasoner Type=\"RuleBased\"/></Action>",
                loaded => loaded.NothingEligibleCycleLimit);
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Curve", "InTangent", curve, CurveSnippet,
                loaded => loaded.AddendCurve!.Keyframes[0].InTangent);
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Curve", "OutTangent", curve, CurveSnippet,
                loaded => loaded.AddendCurve!.Keyframes[0].OutTangent);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Sphere", "Origin", sphere,
                "<Region Type=\"Sphere\" Radius=\"1\"/>", loaded => loaded.Origin);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Capsule", "Origin", capsule,
                "<Region Type=\"Capsule\" Radius=\"1\" HalfHeight=\"1\"/>", loaded => loaded.Origin);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Circle", "Origin", circle,
                "<Region Type=\"Circle\" Radius=\"1\"/>", loaded => loaded.Origin);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Circle", "Rotation", circle,
                "<Region Type=\"Circle\" Radius=\"1\"/>", loaded => loaded.Rotation);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Box", "Origin", new BoxRegion(),
                BareRegionSnippet("Box"), loaded => loaded.Origin);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Box", "Rotation", new BoxRegion(),
                BareRegionSnippet("Box"), loaded => loaded.Rotation);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "ConvexHull", "Origin", new ConvexHullRegion(),
                BareRegionSnippet("ConvexHull"), loaded => loaded.Origin);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "ConvexHull", "Rotation", new ConvexHullRegion(),
                BareRegionSnippet("ConvexHull"), loaded => loaded.Rotation);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Capsule", "Rotation", new CapsuleRegion(),
                BareRegionSnippet("Capsule"), loaded => loaded.Rotation);
            AssertAssignsItsDefault(ConceptualAbstraction.Regions, "Sphere", "Rotation", new SphereRegion(),
                BareRegionSnippet("Sphere"), loaded => loaded.Rotation);

            // A new shape already holds (1, 1, 1) and false, so for these three the row holds the default the
            // description states rather than the reads, which have rows of their own.
            foreach ((string type, Func<RegionBase> create, string _) in BareRegions)
            {
                AssertAssignsItsDefault(ConceptualAbstraction.Regions, type, "Scale", create(), BareRegionSnippet(type),
                    loaded => loaded.Scale);
                AssertAssignsItsDefault(ConceptualAbstraction.Regions, type, "AbsoluteRotation", create(),
                    BareRegionSnippet(type), loaded => loaded.AbsoluteRotation);
                AssertAssignsItsDefault(ConceptualAbstraction.Regions, type, "AbsoluteScale", create(),
                    BareRegionSnippet(type), loaded => loaded.AbsoluteScale);
            }

            AssertAssignsItsDefault(ConceptualAbstraction.Targets, "ByName", "Store", byName,
                "<Target Type=\"ByName\" List=\"Contacts\" Entity=\"Wolf\"/>",
                loaded => ReferenceEquals(loaded.DataStore, brainBlackboard) ? "Brain" : "World", creationData =>
                {
                    creationData.BrainBlackboard = brainBlackboard;
                    creationData.WorldBlackboard = worldBlackboard;
                });
            AssertAssignsItsDefault(ConceptualAbstraction.Actions, "Subreasoner", "DoneWhen", subreasoner,
                "<Action Type=\"Subreasoner\" Name=\"Nest\"><Reasoner Type=\"RuleBased\"/></Action>",
                loaded => loaded.DoneWhen.ToString());
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "Group", "BooleanOperator", group,
                "<Consideration Type=\"Group\"/>", loaded => loaded.BooleanOperator.ToString());
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "Group", "RankOperator", group,
                "<Consideration Type=\"Group\"/>", loaded => loaded.RankOperator.ToString());
            AssertAssignsItsDefault(ConceptualAbstraction.Considerations, "Distance", "IgnoreHeight", distance,
                DistanceSnippet, loaded => loaded.IgnoreHeight);
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Curve", "Linear", curve, CurveSnippet,
                loaded => loaded.AddendCurve!.Keyframes[0].OutTangent == linearSlope);
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "FloatSequence", "RerollOnSelect",
                floatSequence, RolledEntrySnippet, loaded => RerollsOnSelect(loaded),
                creationData => creationData.RandomSource = new FixedRandomSource(0f));
            AssertAssignsItsDefault(ConceptualAbstraction.WeightFunctions, "Constant", "Veto", constant,
                "<WeightFunction Type=\"Constant\"/>", loaded => loaded.Weights.Multiplier == 0f);
        });
    }

    /// <summary>
    ///     Each built-in region, how to make one and the smallest element that loads it, writing none of the attributes
    ///     its bases read.
    /// </summary>
    private static readonly (string type, Func<RegionBase> create, string snippet)[] BareRegions =
    {
        ("Box", () => new BoxRegion(), "<Region Type=\"Box\" Extents=\"(1, 1, 1)\"/>"),
        ("Capsule", () => new CapsuleRegion(), "<Region Type=\"Capsule\" Radius=\"1\" HalfHeight=\"1\"/>"),
        ("Sphere", () => new SphereRegion(), "<Region Type=\"Sphere\" Radius=\"1\"/>"),
        ("ConvexHull", () => new ConvexHullRegion(), """
                                                     <Region Type="ConvexHull">
                                                         <Plane Normal="(1, 0, 0)" Distance="1"/>
                                                         <Plane Normal="(0, 1, 0)" Distance="1"/>
                                                         <Plane Normal="(0, 0, 1)" Distance="1"/>
                                                         <Plane Normal="(-1, -1, -1)" Distance="1"/>
                                                     </Region>
                                                     """),
        ("Circle", () => new CircleRegion(), "<Region Type=\"Circle\" Radius=\"1\"/>"),
        ("Cylinder", () => new CylinderRegion(), "<Region Type=\"Cylinder\" Radius=\"1\" HalfHeight=\"1\"/>"),
        ("Rectangle", () => new RectangleRegion(), "<Region Type=\"Rectangle\" Extents=\"(1, 1)\"/>"),
        ("Polygon", () => new PolygonRegion(), """
                                               <Region Type="Polygon">
                                                   <Corner Position="(0, 0)"/>
                                                   <Corner Position="(1, 0)"/>
                                                   <Corner Position="(0, 1)"/>
                                               </Region>
                                               """),
        ("ExtrudedPolygon", () => new ExtrudedPolygonRegion(), """
                                                               <Region Type="ExtrudedPolygon" HalfHeight="1">
                                                                   <Corner Position="(0, 0)"/>
                                                                   <Corner Position="(1, 0)"/>
                                                                   <Corner Position="(0, 1)"/>
                                                               </Region>
                                                               """),
    };

    /// <summary>A distance between two fixed positions, with IgnoreHeight left unwritten.</summary>
    private const string DistanceSnippet = """
                                           <Consideration Type="Distance">
                                               <From Type="Position" Position="(0, 0, 0)"/>
                                               <To Type="Position" Position="(0, 0, 10)"/>
                                               <WeightFunction Type="Constant"/>
                                           </Consideration>
                                           """;

    /// <summary>
    ///     One entry whose threshold rolls between 0 and 100, with RerollOnSelect left unwritten, and an addend of 7 so
    ///     that the entry's values differ from the neutral default.
    /// </summary>
    private const string RolledEntrySnippet = """
                                              <WeightFunction Type="FloatSequence">
                                                  <Entry ThresholdMin="0" ThresholdMax="100" Addend="7"/>
                                              </WeightFunction>
                                              """;

    /// <summary>A curve of two keyframes with no tangents written, so both tangents take their described defaults.</summary>
    private const string CurveSnippet = """
                                        <WeightFunction Type="Curve">
                                            <AddendCurve>
                                                <Keyframe Input="0" Value="0"/>
                                                <Keyframe Input="10" Value="4"/>
                                            </AddendCurve>
                                        </WeightFunction>
                                        """;

    /// <summary>Asserts, for the node and everything beneath it, that each attribute is one the node's description names.</summary>
    private void AssertEveryAttributeIsDescribed(SpecificationNode node, ComponentDescription description)
    {
        string subject = $"line {node.LineNumber}, the {node.NameSpelling} element";

        foreach (string spelling in node.AttributeSpellings)
        {
            Assert.That(AttributeOf(description, spelling), Is.Not.Null,
                $"{subject} carries {spelling}, which its description does not name");
        }

        foreach (SpecificationNode child in node.Children)
        {
            SlotDescription? slot = SlotOf(description, child.Name);

            Assert.That(slot, Is.Not.Null,
                $"{subject} holds {child.NameSpelling}, which its description has no slot for");

            if (slot == null)
            {
                continue;
            }

            ComponentDescription? childDescription = slot.Inline ?? DescriptionOf(slot.Abstraction!.Value, child.Type);

            Assert.That(childDescription, Is.Not.Null,
                $"line {child.LineNumber}, the {child.NameSpelling} element's type {child.TypeSpelling} is not registered among the {slot.Abstraction}");

            if (childDescription != null)
            {
                AssertEveryAttributeIsDescribed(child, childDescription);
            }
        }
    }

    /// <summary>
    ///     Loads the component from a snippet the attribute is absent from and asserts that what it then holds is the
    ///     description's DefaultText read through the accessor of the attribute's kind.
    /// </summary>
    /// <param name="abstraction">The conceptual abstraction whose factory the type is registered on.</param>
    /// <param name="type">The registered type name whose description carries the attribute.</param>
    /// <param name="spelling">The attribute, which the snippet does not write.</param>
    /// <param name="component">The component the snippet loads into.</param>
    /// <param name="snippet">The configuration to load, writing whatever else the component requires.</param>
    /// <param name="read">What the loaded component holds for the attribute.</param>
    /// <param name="prepare">What to set on the creation data before the load, or null where the defaults serve.</param>
    private void AssertAssignsItsDefault<TComponent>(
        ConceptualAbstraction abstraction,
        AiString type,
        string spelling,
        TComponent component,
        string snippet,
        Func<TComponent, object> read,
        Action<AiCreationData>? prepare = null) where TComponent : IAiComponent
    {
        AttributeDescription? described = AttributeAnywhere(DescriptionOf(abstraction, type)!, spelling);

        Assert.That(described, Is.Not.Null, $"the {type.Text} description names {spelling}");

        if (described == null)
        {
            return;
        }

        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);
        prepare?.Invoke(creationData);

        Assert.That(component.Init(creationData), Is.True, $"the {type.Text} snippet loads");
        Assert.That(read(component), Is.EqualTo(ReadDefault(described)),
            $"the {type.Text} element's {spelling} default \"{described.DefaultText}\" is not what it assigns when {spelling} is absent");
    }

    /// <summary>The smallest element that loads the built-in region of the given type, from BareRegions.</summary>
    private static string BareRegionSnippet(string type)
    {
        return BareRegions.Single(region => region.type == type).snippet;
    }

    /// <summary>Appends every attribute in the description and in the inline descriptions beneath it to the list.</summary>
    private static void CollectAttributes(ComponentDescription description, List<AttributeDescription> attributes)
    {
        attributes.AddRange(description.Attributes);

        foreach (SlotDescription slot in description.Slots)
        {
            if (slot.Inline != null)
            {
                CollectAttributes(slot.Inline, attributes);
            }
        }
    }

    /// <summary>Puts the attribute's default on a node and reads it back through the accessor of the attribute's kind.</summary>
    private static bool ReadsAsItsKind(AttributeDescription attribute)
    {
        SpecificationNode probe = new("Probe");
        probe.SetAttribute(attribute.Spelling, attribute.DefaultText!);

        return attribute.Kind switch
        {
            AttributeKind.Bool => probe.ReadBool(attribute.Name, out _),
            AttributeKind.Int => probe.ReadInt(attribute.Name, out _),
            AttributeKind.Float => probe.ReadFloat(attribute.Name, out _),
            AttributeKind.Vector2 => probe.ReadVector2(attribute.Name, out _),
            AttributeKind.Vector3 => probe.ReadVector3(attribute.Name, out _),
            AttributeKind.Rotation => probe.ReadRotation(attribute.Name, out _),
            AttributeKind.Enumeration => attribute.Choices!.Contains(attribute.DefaultText!),
            _ => probe.ReadString(attribute.Name, out _),
        };
    }

    /// <summary>The attribute's default as the value the accessor of its kind reads from that text.</summary>
    private static object ReadDefault(AttributeDescription attribute)
    {
        SpecificationNode probe = new("Probe");
        probe.SetAttribute(attribute.Spelling, attribute.DefaultText!);

        switch (attribute.Kind)
        {
            case AttributeKind.Int:
                probe.ReadInt(attribute.Name, out int whole);

                return whole;
            case AttributeKind.Float:
                probe.ReadFloat(attribute.Name, out float number);

                return number;
            case AttributeKind.Vector3:
                probe.ReadVector3(attribute.Name, out Vector3 position);

                return position;
            case AttributeKind.Rotation:
                probe.ReadRotation(attribute.Name, out Rotation rotation);

                return rotation;
            case AttributeKind.Bool:
                probe.ReadBool(attribute.Name, out bool flag);

                return flag;
            case AttributeKind.String:
                probe.ReadString(attribute.Name, out string text);

                return text;
            case AttributeKind.Enumeration:
                return attribute.DefaultText!;
            default:
                Assert.Fail($"{attribute.Spelling} is a {attribute.Kind}, whose default this spec does not read");

                return 0f;
        }
    }

    /// <summary>
    ///     Asks whether a loaded function's rolled entry takes a fresh threshold when the function is selected, which is
    ///     what RerollOnSelect holds.
    /// </summary>
    /// <remarks>
    ///     The row loads the function over a source fixed at 0, so the entry's threshold sits at its minimum and an input
    ///     of 50 reads the default. A source fixed at 1 then rolls the threshold to the maximum on Select if the entry
    ///     rerolls, and the same input reads the entry's own values.
    /// </remarks>
    private static bool RerollsOnSelect(FloatSequenceWeightFunction loaded)
    {
        loaded.RandomSource = new FixedRandomSource(1f);
        loaded.Select();

        return loaded.CalculateFloat(50f).Addend == 7f;
    }

    /// <summary>The description registered under the type name in the abstraction's factory, or null.</summary>
    private ComponentDescription? DescriptionOf(ConceptualAbstraction abstraction, AiString type)
    {
        _factories.Factory(abstraction).TryGetDescription(type, out ComponentDescription? description);

        return description;
    }

    /// <summary>The description's attribute of the given name, or null.</summary>
    private static AttributeDescription? AttributeOf(ComponentDescription description, AiString name)
    {
        foreach (AttributeDescription attribute in description.Attributes)
        {
            if (attribute.Name == name)
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>The attribute of the given name in the description or in an inline description beneath it, or null.</summary>
    private static AttributeDescription? AttributeAnywhere(ComponentDescription description, AiString name)
    {
        List<AttributeDescription> attributes = new();
        CollectAttributes(description, attributes);

        foreach (AttributeDescription attribute in attributes)
        {
            if (attribute.Name == name)
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>The description's slot of the given name, or null.</summary>
    private static SlotDescription? SlotOf(ComponentDescription description, AiString name)
    {
        foreach (SlotDescription slot in description.Slots)
        {
            if (slot.Name == name)
            {
                return slot;
            }
        }

        return null;
    }
}
