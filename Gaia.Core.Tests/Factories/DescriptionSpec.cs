using System.Reflection;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers the component descriptions, that each one names every name field its component reads by and no name
///     nothing reads, and that the shipped configurations carry nothing a description does not name.
/// </summary>
/// <remarks>
///     <para>
///         The registered types are reached by reflection, which this spec uses and no other does. Each factory's
///         creators are read from its private _creators field, each creator is run to learn the component's type, and the
///         static <see cref="AiString" /> fields of that type and its bases whose names end in Attribute or Element are
///         the names its Init reads by. A name a component reads through <see cref="WeightValues.TryRead" />,
///         <see cref="AiCreationData.TryReadStore" /> or <see cref="ConsiderationSet.Init" /> is a field on that type
///         instead, so the reflection allows a description to name what those three read and does not require it. A
///         description that leaves out an attribute read through one of the three is caught by the document walks alone,
///         and only where a shipped configuration writes that attribute.
///     </para>
///     <para>
///         <see cref="Pending" /> lists the registered types whose descriptions are not written presently. Each carries
///         a placeholder naming no attribute and no slot, which this spec holds it to, so a type is described and taken
///         off the list in one change.
///     </para>
/// </remarks>
[TestFixture]
public class DescriptionSpec
{
    [SetUp]
    public void BuildSet()
    {
        _factories = new FactorySet();
    }

    /// <summary>The registered types that carry a placeholder description presently, by abstraction and type name.</summary>
    private static readonly (ConceptualAbstraction abstraction, AiString type)[] Pending =
    {
        (ConceptualAbstraction.Considerations, "ConsiderationSet"),
        (ConceptualAbstraction.Considerations, "IntVariable"),
        (ConceptualAbstraction.Considerations, "StringVariable"),
        (ConceptualAbstraction.Regions, "Box"),
        (ConceptualAbstraction.Regions, "Capsule"),
        (ConceptualAbstraction.Regions, "Circle"),
        (ConceptualAbstraction.Regions, "ConvexHull"),
        (ConceptualAbstraction.Regions, "Cylinder"),
        (ConceptualAbstraction.Regions, "ExtrudedPolygon"),
        (ConceptualAbstraction.Regions, "Polygon"),
        (ConceptualAbstraction.Regions, "Rectangle"),
        (ConceptualAbstraction.Regions, "Sphere"),
        (ConceptualAbstraction.Targets, "ByName"),
        (ConceptualAbstraction.WeightFunctions, "String"),
    };

    /// <summary>The types whose name fields are read on a component's behalf, so a description may name what they read.</summary>
    private static readonly Type[] SharedReaders =
    {
        typeof(WeightValues), typeof(AiCreationData), typeof(ConsiderationSet),
    };

    private FactorySet _factories;

    [Test]
    public void NamesEveryFieldARegisteredComponentReadsBy()
    {
        HashSet<(ConceptualAbstraction abstraction, AiString type)> visited = new();

        Assert.Multiple(() =>
        {
            foreach ((ConceptualAbstraction abstraction, AiString type, Type componentType,
                         ComponentDescription description) in Registrations())
            {
                string subject = $"{abstraction} {type.Text}";
                visited.Add((abstraction, type));

                if (Array.IndexOf(Pending, (abstraction, type)) >= 0)
                {
                    Assert.That(description.Attributes, Is.Empty, $"{subject} is pending, so it names no attribute");
                    Assert.That(description.Slots, Is.Empty, $"{subject} is pending, so it names no slot");
                    continue;
                }

                AssertDescribesTheNameFields(componentType, description, subject);
            }

            AssertDescribesTheNameFields(typeof(Brain), _factories.Root, "Root");

            foreach ((ConceptualAbstraction abstraction, AiString type) in Pending)
            {
                Assert.That(visited, Does.Contain((abstraction, type)),
                    $"Pending lists {abstraction} {type.Text}, which is not registered");
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

        foreach ((ConceptualAbstraction _, AiString _, Type _, ComponentDescription description) in Registrations())
        {
            CollectAttributes(description, attributes);
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

    /// <summary>
    ///     Asserts that the component's name fields and the description's names are the same set, the shared readers'
    ///     names allowed.
    /// </summary>
    private static void AssertDescribesTheNameFields(
        Type componentType,
        ComponentDescription description,
        string subject)
    {
        HashSet<AiString> readAttributes = new();
        HashSet<AiString> readElements = new();
        CollectNameFields(componentType, readAttributes, readElements);

        HashSet<AiString> describedAttributes = new();
        HashSet<AiString> describedElements = new();
        CollectDescribedNames(description, describedAttributes, describedElements);

        HashSet<AiString> allowedAttributes = new(readAttributes);
        HashSet<AiString> allowedElements = new(readElements);

        foreach (Type sharedReader in SharedReaders)
        {
            CollectNameFields(sharedReader, allowedAttributes, allowedElements);
        }

        Assert.That(describedAttributes, Is.SupersetOf(readAttributes),
            $"{subject} reads an attribute its description does not name");
        Assert.That(describedElements, Is.SupersetOf(readElements),
            $"{subject} reads an element its description has no slot for");
        Assert.That(describedAttributes, Is.SubsetOf(allowedAttributes),
            $"{subject} describes an attribute nothing reads");
        Assert.That(describedElements, Is.SubsetOf(allowedElements), $"{subject} describes a slot nothing reads");
    }

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
    ///     Every registration on the fixture's set, with the type its creator builds, read through the factories' private
    ///     creators.
    /// </summary>
    private List<(ConceptualAbstraction abstraction, AiString type, Type componentType, ComponentDescription description
        )> Registrations()
    {
        List<(ConceptualAbstraction abstraction, AiString type, Type componentType, ComponentDescription description)>
            registrations = new();

        AddCreations(registrations, ConceptualAbstraction.Actions, _factories.Actions);
        AddCreations(registrations, ConceptualAbstraction.Considerations, _factories.Considerations);
        AddCreations(registrations, ConceptualAbstraction.Options, _factories.Options);
        AddCreations(registrations, ConceptualAbstraction.Reasoners, _factories.Reasoners);
        AddCreations(registrations, ConceptualAbstraction.Regions, _factories.Regions);
        AddCreations(registrations, ConceptualAbstraction.Targets, _factories.Targets);
        AddCreations(registrations, ConceptualAbstraction.WeightFunctions, _factories.WeightFunctions);

        return registrations;
    }

    /// <summary>Runs each of one factory's creators, read by reflection, to learn the type each registered name builds.</summary>
    private void AddCreations<TBase>(
        List<(ConceptualAbstraction abstraction, AiString type, Type componentType, ComponentDescription description)>
            registrations,
        ConceptualAbstraction abstraction,
        ComponentFactory<TBase> factory) where TBase : class, IAiComponent
    {
        FieldInfo? creatorsField = typeof(ComponentFactory<TBase>).GetField("_creators",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(creatorsField, Is.Not.Null, "this spec reads the factory's creators through its _creators field");

        Dictionary<AiString, Func<TBase>> creators =
            (Dictionary<AiString, Func<TBase>>)creatorsField!.GetValue(factory)!;

        foreach (AiString type in factory.Types)
        {
            registrations.Add((abstraction, type, creators[type]().GetType(), DescriptionOf(abstraction, type)!));
        }
    }

    /// <summary>The static AiString fields named XAttribute or XElement on the type and its bases, as the names they hold.</summary>
    private static void CollectNameFields(Type type, ISet<AiString> attributes, ISet<AiString> elements)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Static | BindingFlags.Public |
                                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType != typeof(AiString))
                {
                    continue;
                }

                AiString name = (AiString)field.GetValue(null)!;

                if (field.Name.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    attributes.Add(name);
                }
                else if (field.Name.EndsWith("Element", StringComparison.Ordinal))
                {
                    elements.Add(name);
                }
            }
        }
    }

    /// <summary>Every attribute and slot name in the description and in the inline descriptions beneath it.</summary>
    private static void CollectDescribedNames(
        ComponentDescription description,
        ISet<AiString> attributes,
        ISet<AiString> elements)
    {
        foreach (AttributeDescription attribute in description.Attributes)
        {
            attributes.Add(attribute.Name);
        }

        foreach (SlotDescription slot in description.Slots)
        {
            elements.Add(slot.Name);

            if (slot.Inline != null)
            {
                CollectDescribedNames(slot.Inline, attributes, elements);
            }
        }
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
