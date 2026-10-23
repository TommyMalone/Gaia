using Gaia.Authoring.Descriptions;
using Gaia.Authoring.Documents;
using Gaia.Authoring.Tests.Helpers;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Authoring.Tests.Descriptions;

/// <summary>
///     Covers <see cref="DescriptionResolver" />, every kind of answer it gives for an element of a document, over
///     the shipped factory set.
/// </summary>
/// <remarks>
///     <para>
///         Each test has a factory set of its own, so a registration one test makes reaches no other. Every expected
///         description and slot is taken from that set and compared by reference, so an answer of any other description or
///         slot fails the test. The shipped files are read from the test output folder, and an element of one is found by
///         its name and one attribute that sets it apart from the others of that name, so a line added to the file or
///         taken from it leaves every test looking at the same element. An element of a document a test writes inline is
///         found by the line its opening tag is on, since the test holds the text.
///     </para>
///     <para>
///         A test that needs a type the set lacks reads guard.xml with the Type of the Fight option's action replaced in
///         the text. The piece replaced must occur in the file exactly once, so the test fails, rather than editing the
///         wrong element, when the file changes under it. The replacement is NeverRegistered, which no set registers, or
///         RegisteredLate in the one test that registers it between two asks. Record, the type the shipped files' actions
///         carry, is not used as the unknown type, since it is the one a game registers for them.
///     </para>
/// </remarks>
[TestFixture]
public class ResolverSpec
{
    [SetUp]
    public void MakeTheShippedSet()
    {
        _factories = new FactorySet();
        _logSink = new RecordingLogSink();
    }

    private FactorySet _factories;
    private RecordingLogSink _logSink;

    [Test]
    public void ResolvesTheRootToTheBrainDescription()
    {
        SpecificationNode brain = ReadShipped("sniper.xml");

        Resolution resolution = DescriptionResolver.Resolve(_factories, brain);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(_factories.Root));
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    [Test]
    public void ResolvesAComponentThroughTheSlotItFills()
    {
        SpecificationNode distance = FirstNode(ReadShipped("sniper.xml"), "Consideration", "Type", "Distance");
        ComponentDescription option = DescriptionOf(_factories.Options, "ConsiderationAndAction");

        Resolution resolution = DescriptionResolver.Resolve(_factories, distance);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(DescriptionOf(_factories.Considerations, "Distance")));
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(option, "Consideration")));
        });
    }

    [Test]
    public void ResolvesAStructuralElementToItsInlineDescription()
    {
        SpecificationNode entry = FirstNode(ReadShipped("sniper.xml"), "Entry", "Threshold", "50");
        SlotDescription entrySlot = SlotNamed(DescriptionOf(_factories.WeightFunctions, "FloatSequence"), "Entry");

        Resolution resolution = DescriptionResolver.Resolve(_factories, entry);

        Assert.Multiple(() =>
        {
            Assert.That(entrySlot.Inline, Is.Not.Null, "the FloatSequence Entry slot is described inline");
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(entrySlot.Inline));
            Assert.That(resolution.Slot, Is.SameAs(entrySlot));
        });
    }

    [Test]
    public void FindsTheSlotByItsNameWhenTwoSlotsTakeATarget()
    {
        // The Distance consideration's From and To slots both take a target, and From comes first, so the To slot in
        // the answer shows the slot was found by its name.
        SpecificationNode positionTarget = FirstNode(ReadShipped("sniper.xml"), "To", "Type", "Position");
        ComponentDescription distance = DescriptionOf(_factories.Considerations, "Distance");

        Resolution resolution = DescriptionResolver.Resolve(_factories, positionTarget);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(DescriptionOf(_factories.Targets, "Position")));
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(distance, "To")));
        });
    }

    [Test]
    public void MatchesAnElementNameInAnyCasing()
    {
        const string text = """
                            <Brain>
                                <Reasoner Type="DualUtility">
                                    <option Type="ConsiderationAndAction" Name="Fight"/>
                                </Reasoner>
                            </Brain>
                            """;
        SpecificationNode option = NodeOnLine(Read(text, "lowercase.xml"), 3);

        Resolution resolution = DescriptionResolver.Resolve(_factories, option);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(DescriptionOf(_factories.Options, "ConsiderationAndAction")));
            Assert.That(resolution.Slot,
                Is.SameAs(SlotNamed(DescriptionOf(_factories.Reasoners, "DualUtility"), "Option")));
        });
    }

    [Test]
    public void MatchesTheRootNameInAnyCasing()
    {
        const string text = """
                            <brain>
                                <Reasoner Type="DualUtility"/>
                            </brain>
                            """;
        SpecificationNode root = Read(text, "lowercase_root.xml");

        Resolution resolution = DescriptionResolver.Resolve(_factories, root);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(_factories.Root));
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    [Test]
    public void MatchesATypeInAnyCasing()
    {
        const string text = """
                            <Brain>
                                <Reasoner Type="dualutility"/>
                            </Brain>
                            """;
        SpecificationNode reasoner = NodeOnLine(Read(text, "lowercase_type.xml"), 2);

        Resolution resolution = DescriptionResolver.Resolve(_factories, reasoner);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(DescriptionOf(_factories.Reasoners, "DualUtility")));
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(_factories.Root, "Reasoner")));
        });
    }

    [Test]
    public void ReportsATypeTheFactoryDoesNotRegister()
    {
        SpecificationNode action =
            FirstNode(ReadGuardWithFightActionTyped("NeverRegistered"), "Action", "Name", "Fight");
        ComponentDescription option = DescriptionOf(_factories.Options, "ConsiderationAndAction");

        Resolution resolution = DescriptionResolver.Resolve(_factories, action);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.UnknownType));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(option, "Action")));
        });
    }

    [Test]
    public void ReportsAReasonerTypeTheFactoryDoesNotRegister()
    {
        const string text = """
                            <Brain>
                                <Reasoner Type="NeverRegistered"/>
                            </Brain>
                            """;
        SpecificationNode reasoner = NodeOnLine(Read(text, "unknown_reasoner.xml"), 2);

        Resolution resolution = DescriptionResolver.Resolve(_factories, reasoner);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.UnknownType));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(_factories.Root, "Reasoner")));
        });
    }

    [Test]
    public void ReportsAnActionWithNoType()
    {
        const string text = """
                            <Brain>
                                <Reasoner Type="DualUtility">
                                    <Option Type="ConsiderationAndAction" Name="Fight">
                                        <Action Name="Fight"/>
                                    </Option>
                                </Reasoner>
                            </Brain>
                            """;
        SpecificationNode action = NodeOnLine(Read(text, "untyped_action.xml"), 4);
        ComponentDescription option = DescriptionOf(_factories.Options, "ConsiderationAndAction");

        Resolution resolution = DescriptionResolver.Resolve(_factories, action);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.NoType));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(option, "Action")));
        });
    }

    [Test]
    public void ReportsATypeOnlyAnotherFactoryRegisters()
    {
        // The Consideration slot's abstraction names the considerations factory, and Pause is registered as an action
        // and as nothing else, so that factory has nothing registered under Pause. A resolver that asked every factory
        // in turn would answer the Pause action's description instead.
        SpecificationNode root = ReadShippedWith("sniper.xml", "<Consideration Type=\"Distance\">",
            "<Consideration Type=\"Pause\">");
        SpecificationNode consideration = FirstNode(root, "Consideration", "Type", "Pause");
        ComponentDescription option = DescriptionOf(_factories.Options, "ConsiderationAndAction");

        Resolution resolution = DescriptionResolver.Resolve(_factories, consideration);

        Assert.Multiple(() =>
        {
            Assert.That(_factories.Actions.TryGetDescription("Pause", out _), Is.True,
                "the shipped set registers Pause as an action");
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.UnknownType));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(option, "Consideration")));
        });
    }

    [Test]
    public void ReportsAnElementNoSlotTakes()
    {
        const string text = """
                            <Brain>
                                <Reasoner Type="DualUtility">
                                    <Option Type="ConsiderationAndAction" Name="Fight">
                                        <Junk/>
                                    </Option>
                                </Reasoner>
                            </Brain>
                            """;
        SpecificationNode junk = NodeOnLine(Read(text, "junk.xml"), 4);

        Resolution resolution = DescriptionResolver.Resolve(_factories, junk);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.NoSlot));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    [Test]
    public void ReportsARootThatIsNotABrain()
    {
        const string text = """
                            <Brian>
                                <Reasoner Type="DualUtility"/>
                            </Brian>
                            """;
        SpecificationNode root = Read(text, "brian.xml");

        Resolution resolution = DescriptionResolver.Resolve(_factories, root);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.NoSlot));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    [Test]
    public void ReportsANodeBeneathAnUndescribedOne()
    {
        // The answer for the child comes from the action above it, whose type the set lacks, before the child's own
        // name or Type is looked at. Its Description and Slot are null as a NoSlot answer's are, so Kind alone tells
        // the two apart.
        SpecificationNode action =
            FirstNode(ReadGuardWithFightActionTyped("NeverRegistered"), "Action", "Name", "Fight");
        SpecificationNode child = new("Consideration", "Tuning");
        action.AddChild(child);

        Resolution resolution = DescriptionResolver.Resolve(_factories, child);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.BeneathUndescribed));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    [Test]
    public void AnswersARegistrationMadeAfterTheFirstAsk()
    {
        SpecificationNode action =
            FirstNode(ReadGuardWithFightActionTyped("RegisteredLate"), "Action", "Name", "Fight");
        ResolutionKind kindBeforeTheRegistration = DescriptionResolver.Resolve(_factories, action).Kind;
        List<string> selections = new();

        _factories.Actions.Register("RegisteredLate", () => new RecordAction(selections), RecordAction.Description);
        Resolution resolution = DescriptionResolver.Resolve(_factories, action);

        Assert.Multiple(() =>
        {
            Assert.That(kindBeforeTheRegistration, Is.EqualTo(ResolutionKind.UnknownType));
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(RecordAction.Description));
        });
    }

    [Test]
    public void AnswersANodeWhereAMoveLeftIt()
    {
        // The action starts beneath an option whose type the set lacks, so before the move it answers
        // BeneathUndescribed with no slot. The Action slot it answers after the move can therefore come only from where
        // the move left it.
        const string text = """
                            <Brain>
                                <Reasoner Type="DualUtility">
                                    <Option Type="NeverRegistered" Name="Fight">
                                        <Action Type="Pause" Name="Aim" Duration="2"/>
                                    </Option>
                                    <Option Type="ConsiderationAndAction" Name="Patrol"/>
                                </Reasoner>
                            </Brain>
                            """;
        AuthoringDocument document = new(Read(text, "move.xml"));
        SpecificationNode action = NodeOnLine(document.Root, 4);
        SpecificationNode patrol = NodeOnLine(document.Root, 6);
        ResolutionKind kindBeforeTheMove = DescriptionResolver.Resolve(_factories, action).Kind;
        ComponentDescription option = DescriptionOf(_factories.Options, "ConsiderationAndAction");

        document.Move(action, patrol, 0);
        Resolution resolution = DescriptionResolver.Resolve(_factories, action);

        Assert.Multiple(() =>
        {
            Assert.That(kindBeforeTheMove, Is.EqualTo(ResolutionKind.BeneathUndescribed));
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.Described));
            Assert.That(resolution.Description, Is.SameAs(DescriptionOf(_factories.Actions, "Pause")));
            Assert.That(resolution.Slot, Is.SameAs(SlotNamed(option, "Action")));
        });
    }

    [Test]
    public void ReadsNoneFromADefaultResolution()
    {
        Resolution resolution = default;

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Kind, Is.EqualTo(ResolutionKind.None));
            Assert.That(resolution.Description, Is.Null);
            Assert.That(resolution.Slot, Is.Null);
        });
    }

    /// <summary>Reads one of the shipped files from the test output folder and returns its root.</summary>
    private SpecificationNode ReadShipped(string fileName)
    {
        string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));

        return Read(text, fileName);
    }

    /// <summary>Reads guard.xml with the Type of the Fight option's action replaced by the given type.</summary>
    private SpecificationNode ReadGuardWithFightActionTyped(string type)
    {
        return ReadShippedWith("guard.xml", "<Action Type=\"Record\" Name=\"Fight\"",
            $"<Action Type=\"{type}\" Name=\"Fight\"");
    }

    /// <summary>
    ///     Reads a shipped file with one piece of its text replaced, failing the test unless the piece occurs in the file
    ///     exactly once, so the replacement can land neither on the wrong element nor on more than one.
    /// </summary>
    private SpecificationNode ReadShippedWith(string fileName, string original, string replacement)
    {
        string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));
        int first = text.IndexOf(original, StringComparison.Ordinal);
        int second = first < 0 ? -1 : text.IndexOf(original, first + 1, StringComparison.Ordinal);

        Assert.That(first >= 0 && second < 0, Is.True, $"{fileName} holds {original} exactly once");

        return Read(text.Replace(original, replacement), fileName);
    }

    /// <summary>Reads a document's text and returns its root, failing the test when the reader refuses the text.</summary>
    private SpecificationNode Read(string text, string sourceDocumentName)
    {
        XmlSpecificationReader reader = new(_logSink);
        bool wasRead = reader.TryRead(text, sourceDocumentName, out SpecificationNode? root);

        Assert.That(wasRead, Is.True, string.Join("\n", _logSink.Entries));

        return root!;
    }

    /// <summary>
    ///     Finds the first node, in document order, whose element name and the given attribute's value both match,
    ///     searching the root and everything beneath it and failing the test when there is none.
    /// </summary>
    private static SpecificationNode FirstNode(
        SpecificationNode root,
        string elementName,
        string attribute,
        string value)
    {
        SpecificationNode? found = FindFirst(root, elementName, attribute, value);

        Assert.That(found, Is.Not.Null, $"an element {elementName} has {attribute}=\"{value}\"");

        return found!;
    }

    /// <summary>The first match at or beneath the given node, in document order, or null when there is none.</summary>
    private static SpecificationNode? FindFirst(
        SpecificationNode node,
        string elementName,
        string attribute,
        string value)
    {
        // The reader keeps an element's Type on the node's Type property, not among its other attributes, so Type is
        // read from there.
        string? actual = attribute == "Type" ? node.Type.Text :
            node.TryGetString(attribute, out string read) ? read : null;

        if (node.Name.Text == elementName && actual == value)
        {
            return node;
        }

        foreach (SpecificationNode child in node.Children)
        {
            SpecificationNode? found = FindFirst(child, elementName, attribute, value);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Finds the node whose opening tag is on the given line, searching the root and everything beneath it.</summary>
    private static SpecificationNode NodeOnLine(SpecificationNode root, int lineNumber)
    {
        SpecificationNode? found = FindOnLine(root, lineNumber);

        Assert.That(found, Is.Not.Null, $"an element opens on line {lineNumber}");

        return found!;
    }

    /// <summary>The node on the given line at or beneath the given node, or null when none is.</summary>
    private static SpecificationNode? FindOnLine(SpecificationNode node, int lineNumber)
    {
        if (node.LineNumber == lineNumber)
        {
            return node;
        }

        foreach (SpecificationNode child in node.Children)
        {
            SpecificationNode? found = FindOnLine(child, lineNumber);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The description the factory holds for the type, failing the test when it holds none.</summary>
    private static ComponentDescription DescriptionOf(IComponentFactory factory, string type)
    {
        bool isRegistered = factory.TryGetDescription(type, out ComponentDescription? description);

        Assert.That(isRegistered, Is.True, $"the shipped set registers {type}");

        return description!;
    }

    /// <summary>The slot of the description that takes the given element name.</summary>
    private static SlotDescription SlotNamed(ComponentDescription description, string elementName)
    {
        return description.Slots.Single(slot => slot.Name == elementName);
    }
}
