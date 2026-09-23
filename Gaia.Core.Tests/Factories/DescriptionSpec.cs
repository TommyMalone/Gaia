using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers the component descriptions, that the shipped configurations carry nothing a description does not name,
///     and that the descriptions have the shape the authoring tool reads.
/// </summary>
/// <remarks>
///     A name a component reads for itself is an <see cref="AttributeDescription" /> or
///     <see cref="SlotDescription" /> field made through the component's own <see cref="DescriptionBuilder" />, which
///     records it as it hands it back, so a component's own field is in its description. A piece a base reads on the
///     component's behalf comes from the builder the base hands out, or from an Add the component writes. What is left to
///     assert is what the description says about a name rather than whether it carries one, which is the kind, the
///     choices, the default, the count a slot takes and the abstraction that fills it.
/// </remarks>
[TestFixture]
public class DescriptionSpec
{
    [SetUp]
    public void BuildSet()
    {
        _factories = new FactorySet();
    }

    /// <summary>The seven conceptual abstractions, so that a walk reaches every factory on the set.</summary>
    private static readonly ConceptualAbstraction[] Abstractions =
    {
        ConceptualAbstraction.Actions, ConceptualAbstraction.Considerations, ConceptualAbstraction.Options,
        ConceptualAbstraction.Reasoners, ConceptualAbstraction.Regions, ConceptualAbstraction.Targets,
        ConceptualAbstraction.WeightFunctions,
    };

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

        Assert.Multiple(() =>
        {
            Assert.That(sphere.Attributes[0].Spelling, Is.EqualTo("Origin"));
            Assert.That(sphere.Attributes[1].Spelling, Is.EqualTo("Radius"));
            Assert.That(sphere.Attributes, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void CarriesWhatARegionsBasesReadWithoutTheShapeNamingIt()
    {
        ComponentDescription polygon = DescriptionOf(ConceptualAbstraction.Regions, "Polygon")!;

        Assert.Multiple(() =>
        {
            Assert.That(SlotOf(polygon, "Corner"), Is.Not.Null, "the footprint base reads Corner children");
            Assert.That(AttributeOf(polygon, "Origin"), Is.Not.Null, "the flat shape base reads Origin");
            Assert.That(AttributeOf(polygon, "Rotation"), Is.Not.Null, "the flat shape base reads Rotation");
            Assert.That(polygon.Attributes, Has.Count.EqualTo(2), "a polygon reads no attribute of its own");
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
    public void CarriesTheKindAVariableIsReadAs()
    {
        AttributeDescription floatVariable =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "FloatVariable")!, "Variable")!;
        AttributeDescription entityList =
            AttributeOf(DescriptionOf(ConceptualAbstraction.Considerations, "EntityExists")!, "Variable")!;

        Assert.Multiple(() =>
        {
            Assert.That(floatVariable.Kind, Is.EqualTo(AttributeKind.VariableName));
            Assert.That(floatVariable.VariableKind, Is.EqualTo(DataStoreValueType.Float));
            Assert.That(entityList.VariableKind, Is.EqualTo(DataStoreValueType.EntityList));
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
        });
    }

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
    private void AssertAssignsItsDefault<TComponent>(
        ConceptualAbstraction abstraction,
        AiString type,
        string spelling,
        TComponent component,
        string snippet,
        Func<TComponent, object> read) where TComponent : IAiComponent
    {
        AttributeDescription? described = AttributeAnywhere(DescriptionOf(abstraction, type)!, spelling);

        Assert.That(described, Is.Not.Null, $"the {type.Text} description names {spelling}");

        if (described == null)
        {
            return;
        }

        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        Assert.That(component.Init(creationData), Is.True, $"the {type.Text} snippet loads");
        Assert.That(read(component), Is.EqualTo(ReadDefault(described)),
            $"the {type.Text} element's {spelling} default \"{described.DefaultText}\" is not what it assigns when {spelling} is absent");
    }

    /// <summary>Every attribute in the description and in the inline descriptions beneath it.</summary>
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
            default:
                Assert.Fail($"{attribute.Spelling} is a {attribute.Kind}, whose default this spec does not read");

                return 0f;
        }
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
