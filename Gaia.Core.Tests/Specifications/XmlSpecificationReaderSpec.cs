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
                                       <Region Type="Circle" Center="(0, 0, 150)" Radius="40"/>
                                       <Action Type="Pause" Duration="2"/>
                                   </Option>
                                   """;

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
        bool wasRead = _reader.TryRead("<Option>", SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.False);
            Assert.That(root, Is.Null);
            Assert.That(_logSink.Entries, Has.Count.EqualTo(1));
            Assert.That(_logSink.Entries[0],
                Does.StartWith("Error test.xml Line 1, the document could not be parsed."));
        });
    }

    [Test]
    public void ReportsNothingToReadTheSameWay()
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
}
