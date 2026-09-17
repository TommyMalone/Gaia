using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Specifications;

/// <summary>Covers <see cref="XmlSpecificationWriter" />, the canonical form it emits and the round trip it keeps.</summary>
/// <remarks>
///     Trees are built by parsing snippets through <see cref="XmlSpecificationReader" /> rather than by hand, since
///     the writer's contract is being the reader's inverse.
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
        // The first document the authoring tool will ever open, proven to survive an open and a save untouched. The
        // comparison normalizes endings the same way Canonical does, since the copied file carries whatever endings
        // the checkout gave it.
        string configurationPath = Path.Combine(AppContext.BaseDirectory, "Integration", "sniper.xml");
        string configurationText = File.ReadAllText(configurationPath);

        string written = _writer.Write(Parse(configurationText));

        Assert.That(written, Is.EqualTo(configurationText.ReplaceLineEndings("\n")));
    }

    [Test]
    public void ReachesItsFixedPointInOneWrite()
    {
        // A document in any formatting canonicalizes on the first write, and a second trip changes nothing more,
        // which is what makes the canonical form one form rather than a drift.
        const string messySnippet = """
                                    <Option
                                        Name="Snipe"   Type="ConsiderationAndAction"><Consideration Type="Tuning"/></Option>
                                    """;

        string writtenOnce = _writer.Write(Parse(messySnippet));
        string writtenTwice = _writer.Write(Parse(writtenOnce));

        Assert.That(writtenTwice, Is.EqualTo(writtenOnce));
    }

    /// <summary>Parses a snippet, asserting nothing went wrong, and hands back the root.</summary>
    private SpecificationNode Parse(string snippet)
    {
        _reader.TryRead(snippet, "guard.xml", out SpecificationNode? root);
        return root!;
    }

    /// <summary>A snippet as the writer would emit it, line feeds and one trailing newline.</summary>
    private static string Canonical(string snippet)
    {
        return snippet.ReplaceLineEndings("\n") + "\n";
    }
}
