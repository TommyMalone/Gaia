using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Specifications;

/// <summary>
///     Covers the typed attribute accessors on <see cref="SpecificationNode" />, the silent TryGet tier and the
///     reporting Read tier.
/// </summary>
/// <remarks>
///     The node in the test is parsed through <see cref="XmlSpecificationReader" /> rather than built by hand. The
///     element carries well formed and malformed attributes side by side, and each test reads only the ones it is about.
/// </remarks>
[TestFixture]
public class SpecificationNodeSpec
{
    [SetUp]
    public void ParseTheNode()
    {
        _logSink = new RecordingLogSink();

        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        _node = root!;
    }

    private const string SourceName = "guard.xml";

    private const string Snippet = """
                                   <Region Type="Circle" Name="KillZone" Center="(0, 0, 150)" Radius="40" Ratio="1.5" Attempts="10"
                                           IgnoreHeight="true" Speed="fast" Armed="yes" Offset="(1, 2)" Fraction="1,5"
                                           Corner="(0.5, -2.25)" Facing="(0, 90, 0)"/>
                                   """;

    private RecordingLogSink _logSink;
    private SpecificationNode _node;

    [Test]
    public void ReadsEachKindThroughItsReadAccessor()
    {
        Vector3 killZoneCenter = new(0f, 0f, 150f);

        Assert.Multiple(() =>
        {
            Assert.That(_node.ReadString("Name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("KillZone"));
            Assert.That(_node.ReadFloat("Radius", out float radius), Is.True);
            Assert.That(radius, Is.EqualTo(40f));
            Assert.That(_node.ReadInt("Attempts", out int attempts), Is.True);
            Assert.That(attempts, Is.EqualTo(10));
            Assert.That(_node.ReadBool("IgnoreHeight", out bool ignoreHeight), Is.True);
            Assert.That(ignoreHeight, Is.True);
            Assert.That(_node.ReadVector3("Center", out Vector3 center), Is.True);
            Assert.That(center, Is.EqualTo(killZoneCenter));
            Assert.That(_node.ReadVector2("Corner", out Vector2 corner), Is.True);
            Assert.That(corner, Is.EqualTo(new Vector2(0.5f, -2.25f)));
            Assert.That(_node.ReadRotation("Facing", out Rotation facing), Is.True);
            Assert.That(facing.Forward.X, Is.EqualTo(1f).Within(0.001f), "a yaw of 90 faces right");
            Assert.That(_logSink.Entries, Is.Empty, "well formed reads report nothing");
        });
    }

    [Test]
    public void ReportsARequiredAttributeThatIsMissing()
    {
        bool wasHeightRead = _node.ReadFloat("Height", out _);
        bool wasShelteredRead = _node.ReadBool("Sheltered", out _);
        bool wasLabelRead = _node.ReadString("Label", out _);
        bool wasWaypointRead = _node.ReadVector3("Waypoint", out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasHeightRead, Is.False);
            Assert.That(wasShelteredRead, Is.False);
            Assert.That(wasLabelRead, Is.False);
            Assert.That(wasWaypointRead, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has no Height attribute",
                "Error guard.xml Line 1, the Region element has no Sheltered attribute",
                "Error guard.xml Line 1, the Region element has no Label attribute",
                "Error guard.xml Line 1, the Region element has no Waypoint attribute",
            }));
        });
    }

    [Test]
    public void ReportsAMalformedValueThroughEitherTier()
    {
        bool hasSpeed = _node.TryGetFloat("Speed", out _);
        bool wasArmedRead = _node.ReadBool("Armed", out _);
        bool hasOffset = _node.TryGetVector3("Offset", out _);
        bool wasCenterReadFlat = _node.ReadVector2("Center", out _);
        bool hasOffsetAsRotation = _node.TryGetRotation("Offset", out _);
        bool hasFractionCount = _node.TryGetInt("Fraction", out _);

        Assert.Multiple(() =>
        {
            Assert.That(hasSpeed, Is.False);
            Assert.That(wasArmedRead, Is.False);
            Assert.That(hasOffset, Is.False);
            Assert.That(wasCenterReadFlat, Is.False, "three components is not a point in a plane");
            Assert.That(hasOffsetAsRotation, Is.False);
            Assert.That(hasFractionCount, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                // @formatter:off
                "Error guard.xml Line 1, the Region element's Speed is not a number, it reads \"fast\"",
                "Error guard.xml Line 1, the Region element's Armed is not true or false, it reads \"yes\"",
                "Error guard.xml Line 1, the Region element's Offset is not a tuple like (0, 1.5, 300), it reads \"(1, 2)\"",
                "Error guard.xml Line 1, the Region element's Center is not a tuple like (0, 1.5), it reads \"(0, 0, 150)\"",
                "Error guard.xml Line 1, the Region element's Offset is not pitch, yaw and roll in degrees like (0, 45, 0), it reads \"(1, 2)\"",
                "Error guard.xml Line 1, the Region element's Fraction is not a whole number, it reads \"1,5\"",
                // @formatter:on
            }));
        });
    }

    [Test]
    public void StaysSilentAboutAnOptionalAttributeThatIsAbsent()
    {
        bool hasHeight = _node.TryGetFloat("Height", out _);
        bool hasVisible = _node.TryGetBool("Visible", out _);
        bool hasOrigin = _node.TryGetVector2("Origin", out _);
        bool hasLabel = _node.TryGetString("Label", out _);

        Assert.Multiple(() =>
        {
            Assert.That(hasHeight, Is.False);
            Assert.That(hasVisible, Is.False);
            Assert.That(hasOrigin, Is.False);
            Assert.That(hasLabel, Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ParsesWithInvariantCulture()
    {
        // A comma is a tuple separator rather than a decimal mark, whatever culture the host runs under.
        bool hasRatio = _node.ReadFloat("Ratio", out float ratio);
        bool hasFraction = _node.ReadFloat("Fraction", out _);

        Assert.Multiple(() =>
        {
            Assert.That(hasRatio, Is.True);
            Assert.That(ratio, Is.EqualTo(1.5f));
            Assert.That(hasFraction, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element's Fraction is not a number, it reads \"1,5\"",
            }));
        });
    }

    [Test]
    public void KnowsWhichAttributesTheElementCarries()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_node.HasAttribute("Radius"), Is.True);
            Assert.That(_node.HasAttribute("Missing"), Is.False);
            Assert.That(_logSink.Entries, Is.Empty, "presence checks report nothing");
        });
    }

    [Test]
    public void ReadsABoolInAnyCasing()
    {
        XmlSpecificationReader reader = new(_logSink);

        reader.TryRead("""
                       <Region Marked="True" Guarded="FALSE"/>
                       """, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.ReadBool("Marked", out bool marked), Is.True);
            Assert.That(marked, Is.True);
            Assert.That(root.ReadBool("Guarded", out bool guarded), Is.True);
            Assert.That(guarded, Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }
}
