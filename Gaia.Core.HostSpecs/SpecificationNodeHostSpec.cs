using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves that code outside the assembly can build a <see cref="SpecificationNode" /> tree, edit it, take each
///     edit back, and write it, through the public surface alone.
/// </summary>
/// <remarks>
///     What each member refuses is covered in SpecificationNodeSpec, in Gaia.Core.Tests. What is asserted here is
///     that the public members are enough to take back each edit and write the same text as before it. Some edits need
///     something read back before they can be taken back. A removed child needs its index, a removed attribute its index
///     and spelling, and cleared comments their text.
/// </remarks>
[TestFixture]
public class SpecificationNodeHostSpec
{
    [SetUp]
    public void BuildOption()
    {
        _writer = new XmlSpecificationWriter();

        _option = new SpecificationNode("Option", "ConsiderationAndAction");
        _option.SetAttribute("Name", "Snipe");
        _option.AddLeadingComment(" Shoots from the tower. ");
        _option.AddTrailingComment(" More actions go here. ");

        _aim = new SpecificationNode("Action", "Pause");
        _aim.SetAttribute("Name", "Aim");
        _aim.SetAttribute("Duration", "2");

        _actions = new SpecificationNode("Actions");
        _actions.AddChild(_aim);
        _option.AddChild(_actions);
    }

    private const string WrittenOption = """
                                         <!-- Shoots from the tower. -->
                                         <Option Type="ConsiderationAndAction" Name="Snipe">
                                             <Actions>
                                                 <Action Type="Pause" Name="Aim" Duration="2"/>
                                             </Actions>
                                             <!-- More actions go here. -->
                                         </Option>

                                         """;

    private XmlSpecificationWriter _writer;
    private SpecificationNode _option;
    private SpecificationNode _actions;
    private SpecificationNode _aim;

    [Test]
    public void WritesATreeBuiltInCode()
    {
        string written = _writer.Write(_option);

        Assert.That(written, Is.EqualTo(WrittenOption.ReplaceLineEndings("\n")));
    }

    [Test]
    public void ReadsBackTheTreeItWrote()
    {
        ListLogSink logSink = new();
        XmlSpecificationReader reader = new(logSink);

        bool wasRead = reader.TryRead(_writer.Write(_option), "built.xml", out SpecificationNode? readBack);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True);
            Assert.That(logSink.Entries, Is.Empty);
            Assert.That(readBack!.NameSpelling, Is.EqualTo(_option.NameSpelling));
            Assert.That(readBack.TypeSpelling, Is.EqualTo(_option.TypeSpelling));
            Assert.That(readBack.AttributeSpellings, Is.EqualTo(_option.AttributeSpellings));
            Assert.That(readBack.Children[0].Children[0].Parent, Is.SameAs(readBack.Children[0]));
        });
    }

    [Test]
    public void TakesBackTheChangeOfAValue()
    {
        _aim.TryGetString("Duration", out string valueBefore);

        _aim.SetAttribute("duration", "3");
        string writtenChanged = _writer.Write(_option);
        _aim.SetAttribute("DURATION", valueBefore);

        Assert.Multiple(() =>
        {
            Assert.That(writtenChanged, Is.Not.EqualTo(WrittenOption.ReplaceLineEndings("\n")));
            Assert.That(_writer.Write(_option), Is.EqualTo(WrittenOption.ReplaceLineEndings("\n")),
                "the attribute keeps the spelling and the place it had, whatever casing the change was made under");
        });
    }

    [Test]
    public void TakesBackTheInsertionOfAChild()
    {
        SpecificationNode fire = new("Action", "Fire");
        SpecificationNode reload = new("Action", "Reload");

        _actions.InsertChild(0, fire);
        _actions.InsertChild(1, reload);
        string writtenWithBoth = _writer.Write(_option);
        int reloadIndex = _actions.RemoveChild(reload);
        int fireIndex = _actions.RemoveChild(fire);

        Assert.Multiple(() =>
        {
            Assert.That(writtenWithBoth,
                Does.Contain(
                    "<Action Type=\"Fire\"/>\n        <Action Type=\"Reload\"/>\n        <Action Type=\"Pause\""));
            Assert.That(reloadIndex, Is.EqualTo(1), "the index the child stood at, which an undo inserts it back at");
            Assert.That(fireIndex, Is.EqualTo(0));
            Assert.That(fire.Parent, Is.Null);
            Assert.That(_writer.Write(_option), Is.EqualTo(WrittenOption.ReplaceLineEndings("\n")));
        });
    }

    [Test]
    public void TakesBackTheRemovalOfAnAttribute()
    {
        int nameIndex = _aim.IndexOfAttribute("name");
        string nameSpelling = _aim.AttributeSpellings[nameIndex];
        _aim.TryGetString("name", out string nameValue);

        bool wasRemoved = _aim.RemoveAttribute("name");
        string writtenWithoutName = _writer.Write(_option);
        _aim.InsertAttribute(nameIndex, nameSpelling, nameValue);

        Assert.Multiple(() =>
        {
            Assert.That(wasRemoved, Is.True);
            Assert.That(writtenWithoutName, Does.Contain("<Action Type=\"Pause\" Duration=\"2\"/>"));
            Assert.That(_writer.Write(_option), Is.EqualTo(WrittenOption.ReplaceLineEndings("\n")));
        });
    }

    [Test]
    public void TakesBackTheClearingOfComments()
    {
        List<string> leadingComments = new(_option.LeadingComments);
        List<string> trailingComments = new(_option.TrailingComments);

        _option.ClearLeadingComments();
        _option.ClearTrailingComments();
        string writtenWithoutComments = _writer.Write(_option);

        foreach (string comment in leadingComments)
        {
            _option.AddLeadingComment(comment);
        }

        foreach (string comment in trailingComments)
        {
            _option.AddTrailingComment(comment);
        }

        Assert.Multiple(() =>
        {
            Assert.That(writtenWithoutComments, Does.Not.Contain("<!--"));
            Assert.That(_writer.Write(_option), Is.EqualTo(WrittenOption.ReplaceLineEndings("\n")));
        });
    }

    [Test]
    public void FindsTheNodeAReportNamesFromTheLineItWasWrittenOn()
    {
        _writer.Write(_option, out IReadOnlyDictionary<SpecificationNode, int> lineByNode);

        Assert.Multiple(() =>
        {
            Assert.That(lineByNode[_option], Is.EqualTo(2), "the comment above the option is line 1");
            Assert.That(lineByNode[_actions], Is.EqualTo(3));
            Assert.That(lineByNode[_aim], Is.EqualTo(4));
        });
    }

    [Test]
    public void ReadsAGamesOwnEnumerationThroughEitherTier()
    {
        AttributeDescription stance = AttributeDescription.Enumeration<Stance>("Stance", "Stance", nameof(Stance.Stand),
            "How the character holds itself.");
        ListLogSink logSink = new();
        XmlSpecificationReader reader = new(logSink);
        reader.TryRead("""<Consideration Stance="crouch" Cover="Prone"/>""", "stance.xml",
            out SpecificationNode? consideration);

        bool wasStanceRead = consideration!.ReadEnumeration(stance.Name, out Stance read);
        bool wasCoverRead = consideration.ReadEnumeration("Cover", out Stance _);
        bool hasFallback = consideration.TryGetEnumeration("Fallback", out Stance _);

        Assert.Multiple(() =>
        {
            Assert.That(stance.Choices, Is.EqualTo(new[] { "Crouch", "Stand" }));
            Assert.That(wasStanceRead, Is.True);
            Assert.That(read, Is.EqualTo(Stance.Crouch));
            Assert.That(wasCoverRead, Is.False);
            Assert.That(hasFallback, Is.False, "the attribute is absent, which the silent tier does not report");
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Error stance.xml Line 1, the Consideration element's Cover is not Crouch or Stand, it reads \"Prone\"",
            }));
        });
    }

    [Test]
    public void RefusesFromOutsideWhatItRefusesFromInside()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new SpecificationNode("gaia:Option"), Throws.ArgumentException);
            Assert.That(() => _aim.SetAttribute("Type", "Fire"), Throws.ArgumentException);
            Assert.That(() => _actions.AddChild(_aim), Throws.ArgumentException);
        });
    }

    /// <summary>A game's own enumeration, public in the game's assembly as a host's types are.</summary>
    public enum Stance
    {
        Crouch,
        Stand,
    }
}
