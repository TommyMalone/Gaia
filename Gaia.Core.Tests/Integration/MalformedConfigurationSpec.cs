using Gaia.Core.Factories;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Integration;

/// <summary>Covers loading a configuration with one piece made malformed.</summary>
/// <remarks>
///     <para>
///         A component that logs its child's failure and loads anyway leaves a brain that loads with part of its file
///         missing, so the tests over the shipped files assert on the whole load. Each makes its change to every element
///         the change applies to, one element at a time and each in a fresh parse of the file. The changed tree is written
///         back out and loaded as text. The first error of a refused load is the changed element's own, and each enclosing
///         component's failure follows it. So the first error is the one each test expects to name what the change broke.
///     </para>
///     <para>
///         Which attributes and children a test changes is read from the element's description, the one the factory
///         holds it to. The kind of each attribute, whether it is required and the minimum of each slot all come from
///         there.
///     </para>
/// </remarks>
[TestFixture]
public class MalformedConfigurationSpec
{
    /// <summary>A Type no factory registers.</summary>
    private const string UnregisteredType = "Unregistered";

    /// <summary>Text that reads as no bool, number, tuple or enumeration name.</summary>
    private const string UnreadableText = "unreadable";

    private static readonly string[] ShippedFileNames = { "guard.xml", "sniper.xml" };

    [Test]
    public void RefusesAShippedConfigurationWithAnyOneChildOfAnUnregisteredType()
    {
        AssertEveryChangeFailsTheLoad((element, _, changes) =>
        {
            // The root and the inline elements, such as True and Entry, carry no Type, since no factory builds them.
            if (!element.Type.IsEmpty)
            {
                changes.Add(($"the Type {UnregisteredType}", $"type {UnregisteredType} is not registered", changed =>
                {
                    // A node's Type is fixed when it is built, so a new node takes the element's place. It carries
                    // nothing more, since the factory refuses an unregistered Type before reading anything else.
                    SpecificationNode parent = changed.Parent!;
                    parent.InsertChild(parent.RemoveChild(changed),
                        new SpecificationNode(changed.NameSpelling, UnregisteredType));
                }));
            }
        });
    }

    [Test]
    public void RefusesAShippedConfigurationWithAnyOneAttributeUnreadable()
    {
        AssertEveryChangeFailsTheLoad((element, description, changes) =>
        {
            foreach (string spelling in element.AttributeSpellings)
            {
                AttributeKind kind = AttributeOf(description, spelling)!.Kind;

                // Text and a variable's name read any text, so only an attribute of another kind can be unreadable.
                if (kind != AttributeKind.String && kind != AttributeKind.VariableName)
                {
                    changes.Add(($"{spelling} \"{UnreadableText}\"", $"'s {spelling} is not ",
                        changed => changed.SetAttribute(spelling, UnreadableText)));
                }
            }
        });
    }

    [Test]
    public void RefusesAShippedConfigurationWithAnyOneRequiredChildRemoved()
    {
        AssertEveryChangeFailsTheLoad((element, description, changes) =>
        {
            for (int index = 0; index < element.Children.Count; index++)
            {
                SpecificationNode child = element.Children[index];
                int childIndex = index;

                if (SlotOf(description, child.Name)!.MinCount > 0)
                {
                    changes.Add(($"no {child.NameSpelling} element", child.NameSpelling,
                        changed => changed.RemoveChild(changed.Children[childIndex])));
                }
            }
        });
    }

    [Test]
    public void RefusesAShippedConfigurationWithAnyOneRequiredAttributeRemoved()
    {
        AssertEveryChangeFailsTheLoad((element, description, changes) =>
        {
            foreach (string spelling in element.AttributeSpellings)
            {
                if (AttributeOf(description, spelling)!.IsRequired)
                {
                    changes.Add(($"no {spelling}", $"has no {spelling} attribute",
                        changed => changed.RemoveAttribute(spelling)));
                }
            }
        });
    }

    [Test]
    public void RefusesEachWeightsElementHoldingAnUnreadableValue()
    {
        // Neither shipped file writes a Missing element under a Distance consideration, and neither writes a String
        // weight function, so each is loaded from a snippet of its own.
        const string distanceMissing = """
                                       <Consideration Type="Distance">
                                           <From Type="Self"/>
                                           <To Type="Position" Position="(0, 0, 150)"/>
                                           <Missing Veto="sometimes"/>
                                           <WeightFunction Type="Constant"/>
                                       </Consideration>
                                       """;
        const string stringDefault = """
                                     <Consideration Type="StringVariable" Variable="Stance">
                                         <WeightFunction Type="String">
                                             <Default Veto="sometimes"/>
                                         </WeightFunction>
                                     </Consideration>
                                     """;
        (string snippet, string refusal)[] cases =
        {
            (distanceMissing, "Line 4, the Missing element's Veto is not true or false, it reads \"sometimes\""),
            (stringDefault, "Line 3, the Default element's Veto is not true or false, it reads \"sometimes\""),
        };

        Assert.Multiple(() =>
        {
            foreach ((string snippet, string refusal) in cases)
            {
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                Assert.That(creationData.Factories.Considerations.TryCreate(creationData, out _), Is.False, refusal);
                Assert.That(FirstError(logSink), Is.EqualTo($"Error guard.xml {refusal}"));
            }
        });
    }

    /// <summary>
    ///     Asserts that each shipped file loads as written, and that it fails the whole load with each change the given
    ///     method adds for one of its elements.
    /// </summary>
    /// <remarks>Each change is made to the element at the same place in a fresh parse, so no change meets another.</remarks>
    /// <param name="addChanges">
    ///     Adds the changes that apply to an element, given the element and its description, each as what
    ///     the changed element holds, text its first error carries, and the change itself.
    /// </param>
    private static void AssertEveryChangeFailsTheLoad(
        Action<SpecificationNode, ComponentDescription,
            List<(string holds, string error, Action<SpecificationNode> make)>> addChanges)
    {
        Assert.Multiple(() =>
        {
            foreach (string fileName in ShippedFileNames)
            {
                string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integration", fileName));
                List<(SpecificationNode element, ComponentDescription description)> elements =
                    ElementsOf(text, fileName);

                Assert.That(Loads(text, fileName, new RecordingLogSink()), Is.True, $"{fileName} loads as written");

                for (int index = 0; index < elements.Count; index++)
                {
                    List<(string holds, string error, Action<SpecificationNode> make)> changes = new();
                    addChanges(elements[index].element, elements[index].description, changes);

                    foreach ((string holds, string error, Action<SpecificationNode> make) in changes)
                    {
                        List<(SpecificationNode element, ComponentDescription description)> fresh =
                            ElementsOf(text, fileName);
                        make(fresh[index].element);

                        RecordingLogSink logSink = new();
                        string subject =
                            $"{fileName} line {elements[index].element.LineNumber}, the {elements[index].element.NameSpelling} element with {holds}";

                        Assert.That(Loads(new XmlSpecificationWriter().Write(fresh[0].element), fileName, logSink),
                            Is.False, subject);
                        Assert.That(FirstError(logSink), Does.Contain(error), subject);
                    }
                }
            }
        });
    }

    /// <summary>
    ///     Parses the text and lists every element, the root first and the rest in document order, each with its
    ///     description.
    /// </summary>
    private static List<(SpecificationNode element, ComponentDescription description)> ElementsOf(
        string configurationText,
        string fileName)
    {
        new XmlSpecificationReader(new RecordingLogSink()).TryRead(configurationText, fileName,
            out SpecificationNode? root);

        FactorySet factories = BuildFactories();
        List<(SpecificationNode element, ComponentDescription description)> elements = new();
        Collect(root!, factories.Root, factories, elements);
        return elements;
    }

    /// <summary>Adds the element and then every element beneath it, finding each child's description through its slot.</summary>
    private static void Collect(
        SpecificationNode element,
        ComponentDescription description,
        FactorySet factories,
        List<(SpecificationNode element, ComponentDescription description)> elements)
    {
        elements.Add((element, description));

        foreach (SpecificationNode child in element.Children)
        {
            SlotDescription slot = SlotOf(description, child.Name)!;
            ComponentDescription? childDescription = slot.Inline;

            // A child that is not inline is a component, described under its Type by its slot's factory.
            if (childDescription == null)
            {
                factories.Factory(slot.Abstraction!.Value).TryGetDescription(child.Type, out childDescription);
            }

            Collect(child, childDescription!, factories, elements);
        }
    }

    /// <summary>Loads a brain from the text with the factories the shipped files need.</summary>
    private static bool Loads(string configurationText, string fileName, RecordingLogSink logSink)
    {
        GaiaLoader loader = new(BuildFactories(), new ManualTimeSource(), new Xoshiro128RandomSource(1), logSink);

        return loader.TryLoadBrain(configurationText, fileName, out _);
    }

    /// <summary>The built-in factories with the Record action the shipped files write registered beside them.</summary>
    private static FactorySet BuildFactories()
    {
        FactorySet factories = new();
        factories.Actions.Register("Record", () => new RecordAction(new List<string>()), RecordAction.Description);
        return factories;
    }

    /// <summary>The first error the sink recorded, or empty text when it recorded none.</summary>
    private static string FirstError(RecordingLogSink logSink)
    {
        foreach (string entry in logSink.Entries)
        {
            if (entry.StartsWith("Error"))
            {
                return entry;
            }
        }

        return string.Empty;
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
