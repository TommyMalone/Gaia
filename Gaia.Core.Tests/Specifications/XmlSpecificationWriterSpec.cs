using System.Collections.Concurrent;
using System.Text;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Specifications;

/// <summary>Covers <see cref="XmlSpecificationWriter" />, the canonical form it emits and the round trip it keeps.</summary>
/// <remarks>
///     A snippet already in the canonical form has to write back as the text it came from, and a snippet in any other
///     form writes back in the canonical form, which a second write leaves unchanged. A tree, whether built by hand or
///     read from a document, has to write and read back through <see cref="XmlSpecificationReader" /> as an equal tree.
///     Equal means Describe gives the same text for both trees.
/// </remarks>
[TestFixture]
public class XmlSpecificationWriterSpec
{
    [SetUp]
    public void BuildWriter()
    {
        _logSink = new RecordingLogSink();
        _reader = new XmlSpecificationReader(_logSink);
        _writer = new XmlSpecificationWriter();
    }

    private const string EndOfFact = "\0\n";

    /// <summary>Each way a member takes text, given the text and handing back the node that took it, or throwing.</summary>
    private static readonly Func<string, SpecificationNode>[] BuildAttempts =
    {
        text => new SpecificationNode(text),
        text => new SpecificationNode("a" + text),
        text => new SpecificationNode("a", text),
        text => WithAttributes(text, "v", null),
        text => WithAttributes("b" + text, "v", null),
        text => WithAttributes("v", text, null),
        text => WithAttributes("k", "1", text),
        text => WithComments(text, "x" + text),
    };

    private RecordingLogSink _logSink;
    private XmlSpecificationReader _reader;
    private XmlSpecificationWriter _writer;

    [Test]
    public void WritesACanonicalDocumentBackByteForByte()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Snipe">
                                   <Consideration Type="Tuning" Addend="2"/>
                                   <Action Type="Pause" Name="Aim" Duration="2"/>
                               </Option>
                               """;

        string written = _writer.Write(Parse(snippet));

        Assert.That(written, Is.EqualTo(Canonical(snippet)));
    }

    [Test]
    public void PutsTypeFirstWhateverTheDocumentOrder()
    {
        const string snippet = """
                               <Region Origin="(0, 0, 0)" Type="Circle" Radius="5"/>
                               """;
        const string expected = """
                                <Region Type="Circle" Origin="(0, 0, 0)" Radius="5"/>
                                """;

        string written = _writer.Write(Parse(snippet));

        Assert.That(written, Is.EqualTo(Canonical(expected)));
    }

    [Test]
    public void EscapesReservedCharactersAndSurvivesAReparse()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction" Name="Fish &amp; &quot;Chips&quot;"/>
                               """;

        string written = _writer.Write(Parse(snippet));

        SpecificationNode reparsed = Parse(written);
        reparsed.ReadString("Name", out string name);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(Canonical(snippet)));
            Assert.That(name, Is.EqualTo("Fish & \"Chips\""));
        });
    }

    [Test]
    public void WritesCommentsBackWhereTheyStood()
    {
        const string snippet = """
                               <!-- The sniper's whole brain. -->
                               <Option Type="ConsiderationAndAction" Name="Snipe">
                                   <!-- Vetoes outside the range band. -->
                                   <Consideration Type="Tuning" Addend="2"/>
                                   <!-- More considerations land here. -->
                               </Option>
                               """;

        string written = _writer.Write(Parse(snippet));

        Assert.That(written, Is.EqualTo(Canonical(snippet)));
    }

    [Test]
    public void RoundTripsTheSniperConfigurationCommentsAndAll()
    {
        string configurationPath = Path.Combine(AppContext.BaseDirectory, "Integration", "sniper.xml");
        string configurationText = File.ReadAllText(configurationPath);

        string written = _writer.Write(Parse(configurationText));

        Assert.That(written, Is.EqualTo(configurationText.ReplaceLineEndings("\n")));
    }

    [Test]
    public void ReachesItsFixedPointInOneWrite()
    {
        // A document however it is formatted reaches the canonical form on the first write, and reading that text back
        // and writing it again returns the same text.
        const string messySnippet = """
                                    <Option
                                        Name="Snipe"   Type="ConsiderationAndAction"><Consideration Type="Tuning"/></Option>
                                    """;

        string writtenOnce = _writer.Write(Parse(messySnippet));
        string writtenTwice = _writer.Write(Parse(writtenOnce));

        Assert.That(writtenTwice, Is.EqualTo(writtenOnce));
    }

    [Test]
    public void RoundTripsTheGuardConfigurationCommentsAndAll()
    {
        string configurationPath = Path.Combine(AppContext.BaseDirectory, "Integration", "guard.xml");
        string configurationText = File.ReadAllText(configurationPath);

        string written = _writer.Write(Parse(configurationText));

        Assert.That(written, Is.EqualTo(configurationText.ReplaceLineEndings("\n")));
    }

    [Test]
    public void WritesTypeUnderThatSpellingWhateverTheDocumentUsed()
    {
        string written = _writer.Write(Parse("""<Region Radius="5" tYPE="Circle"/>"""));

        Assert.That(written, Is.EqualTo("<Region Type=\"Circle\" Radius=\"5\"/>\n"));
    }

    [Test]
    public void WritesNoTypeForAnElementThatHasNone()
    {
        // No Type is one state however it was reached, so none of the three writes an empty Type attribute.
        SpecificationNode builtWithoutAType = new("Region");
        SpecificationNode builtFromEmptyText = new("Region", string.Empty);
        SpecificationNode parsedFromEmptyText = Parse("""<Region Type=""/>""");

        Assert.Multiple(() =>
        {
            Assert.That(_writer.Write(builtWithoutAType), Is.EqualTo("<Region/>\n"), "built with no type given");
            Assert.That(_writer.Write(builtFromEmptyText), Is.EqualTo("<Region/>\n"), "built with a type of no text");
            Assert.That(_writer.Write(parsedFromEmptyText), Is.EqualTo("<Region/>\n"), "a parsed Type of no text");
            Assert.That(builtFromEmptyText.Type, Is.EqualTo(AiString.Empty));
            Assert.That(parsedFromEmptyText.Type, Is.EqualTo(AiString.Empty));
        });
    }

    [TestCase("a\tb", "<Region Note=\"a&#x9;b\"/>\n")]
    [TestCase("a\nb", "<Region Note=\"a&#xA;b\"/>\n")]
    [TestCase("a\r\nb", "<Region Note=\"a&#xD;&#xA;b\"/>\n")]
    public void WritesWhitespaceAParserWouldTurnIntoASpaceAsACharacterReference(string value, string expected)
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Note", value);

        string written = _writer.Write(region);

        Parse(written).ReadString("Note", out string readBack);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(expected));
            Assert.That(readBack, Is.EqualTo(value));
        });
    }

    [TestCase("-a")]
    [TestCase("")]
    [TestCase(" a - b ")]
    [TestCase(" one line\n     and another ")]
    public void ReadsBackACommentExactlyAsItWasAdded(string comment)
    {
        SpecificationNode region = new("Region");
        region.AddLeadingComment(comment);
        region.AddTrailingComment(comment);

        SpecificationNode readBack = Parse(_writer.Write(region));

        Assert.Multiple(() =>
        {
            Assert.That(readBack.LeadingComments, Is.EqualTo(new[] { comment }));
            Assert.That(readBack.TrailingComments, Is.EqualTo(new[] { comment }));
        });
    }

    [Test]
    public void ReadsAHandBuiltTreeBackAsAnEqualTree()
    {
        SpecificationNode built = BuildBrain();

        SpecificationNode readBack = Parse(_writer.Write(built));

        Assert.That(Describe(readBack), Is.EqualTo(Describe(built)));
    }

    [Test]
    public void WritesAHandBuiltTreeTheSameAgainOnceItHasBeenReadBack()
    {
        string writtenOnce = _writer.Write(BuildBrain());

        string writtenTwice = _writer.Write(Parse(writtenOnce));

        Assert.That(writtenTwice, Is.EqualTo(writtenOnce));
    }

    [Test]
    public void WritesEachDocumentInItsOwnSpellingsWhateverAnotherDocumentUsed()
    {
        // The two documents spell every name and the Type differently, and the string table keeps whichever spelling it
        // met first. Whichever that was, one of the two would be written in the other's spelling if any name's text
        // came from the table.
        const string firstDocument =
            "<ZzProbeRegion Type=\"ZzProbeCircle\" ZzProbeRadius=\"4\">\n    <ZzProbeOrigin/>\n</ZzProbeRegion>\n";
        const string secondDocument =
            "<zzprobeREGION Type=\"zzprobeCIRCLE\" zzprobeRADIUS=\"4\">\n    <zzprobeORIGIN/>\n</zzprobeREGION>\n";

        SpecificationNode firstRoot = Parse(firstDocument);
        SpecificationNode secondRoot = Parse(secondDocument);

        Assert.Multiple(() =>
        {
            Assert.That(_writer.Write(firstRoot), Is.EqualTo(firstDocument));
            Assert.That(_writer.Write(secondRoot), Is.EqualTo(secondDocument));
        });
    }

    [Test]
    public void ReportsTheLinesAReaderOfTheWrittenTextRecords()
    {
        SpecificationNode brain = BuildBrain();

        string written = _writer.Write(brain, out IReadOnlyDictionary<SpecificationNode, int> lineByNode);

        SpecificationNode readBack = Parse(written);
        List<int> writtenLines = new();
        List<int> readLines = new();
        CollectLines(brain, node => lineByNode[node], writtenLines);
        CollectLines(readBack, node => node.LineNumber, readLines);

        Assert.That(writtenLines, Is.EqualTo(readLines));
    }

    [Test]
    public void WritesTheSameTextWhetherOrNotTheLinesAreAskedFor()
    {
        SpecificationNode brain = BuildBrain();

        string writtenWithLines = _writer.Write(brain, out IReadOnlyDictionary<SpecificationNode, int> _);

        Assert.That(writtenWithLines, Is.EqualTo(_writer.Write(brain)));
    }

    [Test]
    [Category(XmlSpecificationReaderSpec.SweepCategory)]
    public void ReadsEveryDocumentTheReaderAcceptsBackAsAnEqualTree()
    {
        // Reads every document XmlSpecificationReaderSpec sweeps and keeps the ones the reader accepts. Each accepted
        // tree is written, the text is read back, and the tree read back has to equal the tree it was written from and
        // write the same text again.
        ConcurrentQueue<string> failures = new();
        int acceptedCount = 0;

        Parallel.For(0, XmlSpecificationReaderSpec.SweepShapes.Length * 0x10000, index =>
        {
            char character = (char)(index % 0x10000);
            string shape = XmlSpecificationReaderSpec.SweepShapes[index / 0x10000];
            string document = shape.Replace("{c}", character.ToString()).Replace("{x}", ((int)character).ToString("X"));

            RecordingLogSink logSink = new();
            XmlSpecificationReader reader = new(logSink);
            XmlSpecificationWriter writer = new();

            if (!reader.TryRead(document, "swept.xml", out SpecificationNode? root))
            {
                return;
            }

            Interlocked.Increment(ref acceptedCount);

            string written = writer.Write(root!);

            if (!reader.TryRead(written, "written.xml", out SpecificationNode? readBack))
            {
                failures.Enqueue($"U+{(int)character:X4} in {shape} was written as text the reader refused");
            }
            else if (Describe(readBack!) != Describe(root!))
            {
                failures.Enqueue($"U+{(int)character:X4} in {shape} read back as a different tree");
            }
            else if (writer.Write(readBack!) != written)
            {
                failures.Enqueue($"U+{(int)character:X4} in {shape} wrote different text the second time");
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(failures, Is.Empty);
            Assert.That(acceptedCount, Is.GreaterThan(0x10000), "the sweep is of accepted documents, so most must be");
        });
    }

    [Test]
    public void ReadsBackAValueHoldingACharacterOutsideTheFirstPlane()
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Note", "a\uD801\uDC00b");

        Parse(_writer.Write(region)).ReadString("Note", out string readBack);

        Assert.That(readBack, Is.EqualTo("a\uD801\uDC00b"));
    }

    [Test]
    [Category(XmlSpecificationReaderSpec.SweepCategory)]
    public void ReadsEveryTreeTheMembersAcceptBackAsAnEqualTree()
    {
        // Every character a char can hold is offered to each member that takes text, alone and after a letter in an
        // element or attribute name, as a Type, as an attribute value, as the name of a second attribute, and in a
        // comment above and inside an element. Whatever the members accept is written and read back. A member that
        // accepted text the writer cannot write or the reader cannot give back would fail here.
        ConcurrentQueue<string> failures = new();
        int acceptedCount = 0;

        Parallel.For(0, BuildAttempts.Length * 0x10000, index =>
        {
            char character = (char)(index % 0x10000);
            int attempt = index / 0x10000;
            SpecificationNode built;

            try
            {
                built = BuildAttempts[attempt](character.ToString());
            }
            catch (ArgumentException)
            {
                return;
            }

            Interlocked.Increment(ref acceptedCount);

            RecordingLogSink logSink = new();
            XmlSpecificationReader reader = new(logSink);
            XmlSpecificationWriter writer = new();

            if (!reader.TryRead(writer.Write(built), "built.xml", out SpecificationNode? readBack))
            {
                failures.Enqueue($"U+{(int)character:X4} in attempt {attempt} was written as text the reader refused");
            }
            else if (Describe(readBack!) != Describe(built))
            {
                failures.Enqueue($"U+{(int)character:X4} in attempt {attempt} read back as a different tree");
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(failures, Is.Empty);
            Assert.That(acceptedCount, Is.GreaterThan(0x10000), "the sweep is of accepted text, so most must be");
        });
    }

    /// <summary>Parses a snippet, asserting nothing went wrong, and hands back the root.</summary>
    private SpecificationNode Parse(string snippet)
    {
        bool wasRead = _reader.TryRead(snippet, "guard.xml", out SpecificationNode? root);

        Assert.That(wasRead, Is.True, "the snippet parses");

        return root!;
    }

    /// <summary>A snippet as the writer would emit it, line feeds and one trailing newline.</summary>
    private static string Canonical(string snippet)
    {
        return snippet.ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    ///     Builds a brain by hand that holds everything a tree can, comments above and inside elements, a Type,
    ///     attributes, children, an element with no Type, and a subreasoner nested under an action.
    /// </summary>
    private static SpecificationNode BuildBrain()
    {
        SpecificationNode brain = new("Brain");
        brain.AddLeadingComment(" A hand-built brain. ");
        brain.AddLeadingComment(" Its second comment\n     runs over two lines. ");

        SpecificationNode reasoner = new("Reasoner", "DualUtility");
        reasoner.AddLeadingComment(" The root reasoner. ");

        SpecificationNode option = new("Option", "ConsiderationAndAction");
        option.SetAttribute("Name", "Fish & \"Chips\"");
        option.SetAttribute("Note", "tab\there, <angle> and\r\nline");

        SpecificationNode consideration = new("Consideration", "Tuning");
        consideration.SetAttribute("Addend", "2");

        SpecificationNode action = new("Action", "Subreasoner");
        action.SetAttribute("Name", "Steps");

        SpecificationNode subreasoner = new("Reasoner", "Sequence");
        subreasoner.AddTrailingComment(" Steps go here. ");

        SpecificationNode lastOption = new("Option", "ConsiderationAndAction");
        lastOption.SetAttribute("Name", "Idle");

        brain.AddChild(reasoner);
        reasoner.AddChild(option);
        option.AddChild(consideration);
        option.AddChild(action);
        action.AddChild(subreasoner);
        reasoner.AddChild(lastOption);
        reasoner.AddTrailingComment(" More options go here. ");

        return brain;
    }

    /// <summary>
    ///     Spells out everything a tree holds, one line per fact, without going through the writer, so two trees are
    ///     equal when their descriptions are.
    /// </summary>
    /// <remarks>
    ///     Each fact ends with the character numbered 0, which no text in a tree can hold, so a value holding a line feed
    ///     cannot pass for a second fact.
    /// </remarks>
    private static string Describe(SpecificationNode node)
    {
        StringBuilder description = new();
        Describe(node, 0, description);
        return description.ToString();
    }

    private static void Describe(SpecificationNode node, int depth, StringBuilder description)
    {
        foreach (string comment in node.LeadingComments)
        {
            description.Append(depth).Append(" above ").Append(comment).Append(EndOfFact);
        }

        description.Append(depth).Append(" element ").Append(node.NameSpelling).Append(" type ")
            .Append(node.TypeSpelling).Append(" same identity ")
            .Append(node.Name == new AiString(node.NameSpelling) && node.Type == new AiString(node.TypeSpelling))
            .Append(EndOfFact);

        foreach (string spelling in node.AttributeSpellings)
        {
            node.TryGetString(spelling, out string value);
            description.Append(depth).Append(" attribute ").Append(spelling).Append(" = ").Append(value)
                .Append(EndOfFact);
        }

        foreach (SpecificationNode child in node.Children)
        {
            description.Append(depth).Append(" child with this parent ").Append(child.Parent == node).Append(EndOfFact);
            Describe(child, depth + 1, description);
        }

        foreach (string comment in node.TrailingComments)
        {
            description.Append(depth).Append(" inside ").Append(comment).Append(EndOfFact);
        }
    }

    /// <summary>Builds a node with one attribute, and a second set after it when a second name is given.</summary>
    private static SpecificationNode WithAttributes(string name, string value, string? secondName)
    {
        SpecificationNode node = new("a");
        node.SetAttribute(name, value);

        if (secondName != null)
        {
            node.SetAttribute(secondName, "2");
        }

        return node;
    }

    /// <summary>Builds a node with one comment above it and one inside it.</summary>
    private static SpecificationNode WithComments(string leadingComment, string trailingComment)
    {
        SpecificationNode node = new("a");
        node.AddLeadingComment(leadingComment);
        node.AddTrailingComment(trailingComment);
        return node;
    }

    /// <summary>Gathers one line per node, parents before children, so two trees of one shape line up entry for entry.</summary>
    private static void CollectLines(SpecificationNode node, Func<SpecificationNode, int> lineOf, List<int> lines)
    {
        lines.Add(lineOf(node));

        foreach (SpecificationNode child in node.Children)
        {
            CollectLines(child, lineOf, lines);
        }
    }
}
