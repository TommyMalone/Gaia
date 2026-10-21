using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Specifications;

/// <summary>Covers <see cref="XmlSpecificationReader" />, the trees it builds and the documents it refuses.</summary>
/// <remarks>
///     Reports are observed through a <see cref="RecordingLogSink" /> and are filed under the source name the reader
///     was handed, which is what the expected entries' channel shows.
/// </remarks>
[TestFixture]
public class XmlSpecificationReaderSpec
{
    [SetUp]
    public void BuildReader()
    {
        _logSink = new RecordingLogSink();
        _reader = new XmlSpecificationReader(_logSink);
    }

    private const string SourceName = "test.xml";

    private const string Snippet = """
                                   <Option Type="ConsiderationAndAction" Name="Snipe">
                                       <!-- The marketplace kill zone. -->
                                       <Region Type="Circle" Origin="(0, 0, 150)" Radius="40"/>
                                       <Action Type="Pause" Duration="2"/>
                                   </Option>
                                   """;

    /// <summary>The category of a test that tries every character, which takes seconds where the others take milliseconds.</summary>
    /// <remarks>
    ///     A run that leaves the category out, with the filter TestCategory!=Sweep, skips them, and a run before landing
    ///     keeps them.
    /// </remarks>
    internal const string SweepCategory = "Sweep";

    /// <summary>The documents the sweeps of this spec and of <see cref="XmlSpecificationWriterSpec" /> fill in.</summary>
    /// <remarks>Each sweep replaces {c} with a character as itself and {x} with the same character's number in hexadecimal.</remarks>
    internal static readonly string[] SweepShapes =
    {
        "<{c}/>",
        "<a{c}/>",
        "<a {c}=\"v\"/>",
        "<a b{c}=\"v\"/>",
        "<a v=\"{c}\"/>",
        "<a v=\"&#x{x};\"/>",
        "<a Type=\"{c}\"/>",
        "<a Type=\"&#x{x};\"/>",
        "<!--{c}--><a/>",
        "<a><!--{c}--><b/></a>",
        "<a><!--x{c}--></a>",
        "<a>{c}</a>",
        "<a {c}=\"1\" k=\"2\"/>",
        "<a t{c}pe=\"1\" Type=\"2\"/>",
        "<{c}:a xmlns:{c}=\"urn:p\"/>",
        "<a xmlns:p=\"urn:p\" p:{c}=\"1\"/>",
        "<!DOCTYPE a [<!ATTLIST a {c} CDATA \"1\">]><a k=\"2\"/>",
        "<!DOCTYPE a [<!ENTITY e \"{c}\">]><a v=\"&e;\"/>",
    };

    private RecordingLogSink _logSink;
    private XmlSpecificationReader _reader;

    [Test]
    public void BuildsTheTreeTheDocumentDescribes()
    {
        bool wasRead = _reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        AiString optionName = "Option";
        AiString optionType = "ConsiderationAndAction";
        AiString regionType = "Circle";
        AiString actionType = "Pause";

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True);
            Assert.That(root!.Name, Is.EqualTo(optionName));
            Assert.That(root.Type, Is.EqualTo(optionType));
            Assert.That(root.Children, Has.Count.EqualTo(2));
            Assert.That(root.Children[0].Type, Is.EqualTo(regionType));
            Assert.That(root.Children[1].Type, Is.EqualTo(actionType));
        });
    }

    [Test]
    public void SeparatesTheTypeFromTheOtherAttributes()
    {
        _reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        SpecificationNode region = root!.Children[0];

        Assert.Multiple(() =>
        {
            Assert.That(region.TryGetString("Type", out _), Is.False, "the discriminator is not an attribute");
            Assert.That(region.TryGetFloat("Radius", out float radius), Is.True);
            Assert.That(radius, Is.EqualTo(40f));
        });
    }

    [Test]
    public void AttachesACommentToTheElementBelowIt()
    {
        _reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.Children, Has.Count.EqualTo(2), "the comment is not a child");
            Assert.That(root.Children[0].LeadingComments, Is.EqualTo(new[] { " The marketplace kill zone. " }));
            Assert.That(root.Children[1].LeadingComments, Is.Empty);
            Assert.That(_logSink.Entries, Is.Empty, "the comment is not stray text");
        });
    }

    [Test]
    public void KeepsEveryCommentInARunAboveAnElement()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction">
                                   <!-- Waits out the cooldown. -->
                                   <!-- Two seconds suits the guard. -->
                                   <Action Type="Pause" Duration="2"/>
                               </Option>
                               """;

        _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.That(root!.Children[0].LeadingComments,
            Is.EqualTo(new[] { " Waits out the cooldown. ", " Two seconds suits the guard. " }));
    }

    [Test]
    public void AttachesATrailingCommentToItsContainingElement()
    {
        const string snippet = """
                               <Option Type="ConsiderationAndAction">
                                   <Action Type="Pause" Duration="2"/>
                                   <!-- More actions land here. -->
                               </Option>
                               """;

        _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.TrailingComments, Is.EqualTo(new[] { " More actions land here. " }));
            Assert.That(root.Children[0].LeadingComments, Is.Empty);
        });
    }

    [Test]
    public void AttachesACommentAboveTheRootToTheRoot()
    {
        const string snippet = """
                               <!-- The guard's whole brain. -->
                               <Brain/>
                               """;

        _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.That(root!.LeadingComments, Is.EqualTo(new[] { " The guard's whole brain. " }));
    }

    [Test]
    public void WarnsAboutACommentAfterTheRootElement()
    {
        const string snippet = """
                               <Brain/>
                               <!-- Nothing below can carry this. -->
                               """;

        bool wasRead = _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True, "a lost comment does not fail the read");
            Assert.That(root!.LeadingComments, Is.Empty);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Warning test.xml Line 2, the comment after the root element is not kept",
            }));
        });
    }

    [Test]
    public void RecordsTheLineEachElementStartedOn()
    {
        _reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.LineNumber, Is.EqualTo(1));
            Assert.That(root.Children[0].LineNumber, Is.EqualTo(3));
        });
    }

    [Test]
    public void ReportsAMalformedDocumentAndYieldsNoTree()
    {
        XmlException thrown = Assert.Throws<XmlException>(() => XDocument.Parse("<Option>", LoadOptions.SetLineInfo))!;

        bool wasRead = _reader.TryRead("<Option>", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(thrown.LineNumber, Is.EqualTo(1));
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Error test.xml Line 1, the document could not be parsed. {thrown.Message}",
            }));
        });
    }

    [Test]
    public void ReportsEmptyAndBlankTextAndYieldsNoTree()
    {
        bool wasEmptyRead = _reader.TryRead(string.Empty, SourceName, out SpecificationNode? emptyRoot);
        bool wasBlankRead = _reader.TryRead("   ", SourceName, out SpecificationNode? blankRoot);

        Assert.Multiple(() =>
        {
            Assert.That(wasEmptyRead, Is.False);
            Assert.That(wasBlankRead, Is.False);
            Assert.That(emptyRoot, Is.Null);
            Assert.That(blankRoot, Is.Null);
            Assert.That(_logSink.Entries, Has.Count.EqualTo(2));
        });
    }

    // @formatter:off
    [TestCase("")]
    [TestCase("<?xml version=\"1.0\"?>")]
    [TestCase("\n\n<!-- A brain is yet to be written here. -->")]
    // @formatter:on
    public void ReportsADocumentWithNoRootElementUsingTheParsersMessageAndNoLine(string document)
    {
        // XDocument.Parse refuses text with no root element itself, at line 0 wherever the text ends. No file has a
        // line 0, so the reader's entry names no line and carries the parser's message after its own. The parser is
        // asked first, so a runtime that threw at another line fails the assertion on its line rather than reading as
        // the reader's, and the entry is expected to carry whatever message that runtime gives.
        XmlException thrown = Assert.Throws<XmlException>(() => XDocument.Parse(document, LoadOptions.SetLineInfo))!;

        bool wasRead = _reader.TryRead(document, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(thrown.LineNumber, Is.Zero);
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Error test.xml The document could not be parsed. {thrown.Message}",
            }));
        });
    }

    [Test]
    public void WarnsAboutStrayTextInsideAnElement()
    {
        bool wasRead = _reader.TryRead("<Option>oops</Option>", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True, "stray text does not fail the read");
            Assert.That(root, Is.Not.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Warning test.xml Line 1, the Option element holds stray text \"oops\"",
            }));
        });
    }

    [Test]
    public void TakesTheTypeInAnyCasing()
    {
        _reader.TryRead("""<Region type="Circle" Radius="4"/>""", SourceName, out SpecificationNode? root);

        AiString circleType = "Circle";

        Assert.Multiple(() =>
        {
            Assert.That(root!.Type, Is.EqualTo(circleType));
            Assert.That(root.AttributeSpellings, Is.EqualTo(new[] { "Radius" }),
                "the discriminator is not an attribute");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void KeepsTheSpellingsTheDocumentUsed()
    {
        _reader.TryRead("""<rEGION Type="cIRCLE" rADIUS="4"/>""", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.NameSpelling, Is.EqualTo("rEGION"));
            Assert.That(root.TypeSpelling, Is.EqualTo("cIRCLE"));
            Assert.That(root.AttributeSpellings, Is.EqualTo(new[] { "rADIUS" }));
        });
    }

    [Test]
    public void GivesEachParsedChildItsParent()
    {
        _reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.Parent, Is.Null);
            Assert.That(root.Children[0].Parent, Is.SameAs(root));
            Assert.That(root.Children[1].Parent, Is.SameAs(root));
        });
    }

    // @formatter:off
    [TestCase("""<Region Radius="4" radius="5"/>""",
        "Error test.xml Line 1, the Region element carries Radius twice, the second time as radius")]
    [TestCase("<Option>\n    <Region Radius=\"4\" radius=\"5\"/>\n</Option>",
        "Error test.xml Line 2, the Region element carries Radius twice, the second time as radius")]
    [TestCase("""<Region Type="Circle" type="Sphere"/>""",
        "Error test.xml Line 1, the Region element carries Type twice, the second time as type")]
    [TestCase("<Option>\n    <Region Type=\"Circle\" type=\"Sphere\"/>\n</Option>",
        "Error test.xml Line 2, the Region element carries Type twice, the second time as type")]
    // @formatter:on
    public void RefusesAnElementThatCarriesOneNameInTwoSpellings(string document, string expectedEntry)
    {
        bool wasRead = _reader.TryRead(document, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[] { expectedEntry }));
        });
    }

    [Test]
    public void RefusesADefaultNamespaceDeclaration()
    {
        bool wasRead = _reader.TryRead("""<Region xmlns="urn:x"><Origin/></Region>""", SourceName,
            out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error test.xml Line 1, the Region element declares the XML namespace \"urn:x\", and Gaia reads no XML namespace",
            }), "the elements the declaration covers are not reported again");
        });
    }

    [Test]
    public void RefusesAPrefixDeclaration()
    {
        bool wasRead = _reader.TryRead("""<Region xmlns:p="urn:p"/>""", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error test.xml Line 1, the Region element declares the XML namespace \"urn:p\", and Gaia reads no XML namespace",
            }));
        });
    }

    [Test]
    public void RefusesAnAttributeWrittenWithAPrefix()
    {
        const string snippet = """
                               <Brain xmlns:p="urn:p">
                                   <Region p:Radius="4"/>
                               </Brain>
                               """;

        bool wasRead = _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries,
                Does.Contain(
                    "Error test.xml Line 2, the Region element's Radius is in the XML namespace \"urn:p\", and Gaia reads no XML namespace"));
        });
    }

    [Test]
    public void RefusesAnElementWrittenWithAPrefix()
    {
        const string snippet = """
                               <Brain xmlns:p="urn:p">
                                   <p:Region Radius="4"/>
                               </Brain>
                               """;

        bool wasRead = _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries,
                Does.Contain(
                    "Error test.xml Line 2, the Region element is in the XML namespace \"urn:p\", and Gaia reads no XML namespace"));
        });
    }

    [Test]
    public void RefusesATypeWrittenWithAPrefix()
    {
        const string snippet = """
                               <Region xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:type="Circle"/>
                               """;

        bool wasRead = _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries,
                Does.Contain(
                    "Error test.xml Line 1, the Region element's type is in the XML namespace \"http://www.w3.org/2001/XMLSchema-instance\", and Gaia reads no XML namespace"));
        });
    }

    [Test]
    public void RefusesAnElementNamedXmlns()
    {
        bool wasRead = _reader.TryRead("<Brain><xmlns/></Brain>", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error test.xml Line 1, the xmlns element's name is what XML declares a namespace with, and Gaia reads no XML namespace",
            }));
        });
    }

    [Test]
    public void TakesXmlnsInAnotherCasingAsAnOrdinaryName()
    {
        bool wasRead = _reader.TryRead("""<XMLNS Xmlns="urn:x"/>""", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True);
            Assert.That(root!.NameSpelling, Is.EqualTo("XMLNS"));
            Assert.That(root.AttributeSpellings, Is.EqualTo(new[] { "Xmlns" }));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ReportsWhatIsRefusedBeneathARefusedElementInTheSameRead()
    {
        const string snippet = """
                               <Brain xmlns="urn:x">
                                   <Region Radius="4" radius="5"/>
                                   <Region Height="4" HEIGHT="5"/>
                               </Brain>
                               """;

        _reader.TryRead(snippet, SourceName, out SpecificationNode? _);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            "Error test.xml Line 1, the Brain element declares the XML namespace \"urn:x\", and Gaia reads no XML namespace",
            "Error test.xml Line 2, the Region element carries Radius twice, the second time as radius",
            "Error test.xml Line 3, the Region element carries Height twice, the second time as HEIGHT",
        }));
    }

    [Test]
    public void RefusesADocumentTypeDefinition()
    {
        // A definition can declare an attribute default the text never writes, which would load and be saved back
        // into the file, so the document is refused at the definition and no tree carries the default.
        const string snippet = """
                               <!DOCTYPE Brain [<!ATTLIST Entry Veto CDATA "true">]>
                               <Brain>
                                   <Entry/>
                               </Brain>
                               """;

        bool wasRead = _reader.TryRead(snippet, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error test.xml Line 1, the document declares a document type, and Gaia reads no document type definition",
            }));
        });
    }

    [Test]
    [Category(SweepCategory)]
    public void ReturnsWithoutThrowingWhateverCharacterStandsAnywhereInADocument()
    {
        // Every value a char can take, the lone halves of a pair included, is put in the marked place of each of
        // SweepShapes. The shapes cover an element name, an attribute name, a prefix, a value and a Type as the
        // character and as a character reference, a comment in each place the reader keeps one, stray text, an
        // attribute name beside k and beside Type that may be the other's in another casing, and an attribute name
        // and an entity that a document type declares. A refused read has to log an error that names the reason. That
        // error must not come from the catch in TryRead, which reports an ArgumentException a SpecificationNode member
        // threw. Reaching that catch means the parser accepted something a member refuses and the reader called the
        // member without checking first, the way it checks NameProblem before the constructor and IndexOfAttribute
        // before SetAttribute.
        ConcurrentQueue<string> failures = new();

        Parallel.For(0, SweepShapes.Length * 0x10000, index =>
        {
            char character = (char)(index % 0x10000);
            string document = SweepShapes[index / 0x10000].Replace("{c}", character.ToString())
                .Replace("{x}", ((int)character).ToString("X"));

            RecordingLogSink logSink = new();
            XmlSpecificationReader reader = new(logSink);

            try
            {
                bool wasRead = reader.TryRead(document, SourceName, out SpecificationNode? root);

                if (wasRead != (root != null))
                {
                    failures.Enqueue(
                        $"U+{(int)character:X4} in {SweepShapes[index / 0x10000]} returned {wasRead} beside its tree");
                }

                if (!wasRead && logSink.Entries.Count == 0)
                {
                    failures.Enqueue($"U+{(int)character:X4} in {SweepShapes[index / 0x10000]} was refused in silence");
                }

                foreach (string entry in logSink.Entries)
                {
                    if (entry.Contains("could not be built"))
                    {
                        failures.Enqueue(
                            $"U+{(int)character:X4} in {SweepShapes[index / 0x10000]} reached the guard, {entry}");
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue(
                    $"U+{(int)character:X4} in {SweepShapes[index / 0x10000]} threw {exception.GetType().Name}");
            }
        });

        Assert.That(failures, Is.Empty);
    }
}
