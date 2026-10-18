using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers the loading of every region type, refusing an element with one value that does not read.</summary>
/// <remarks>
///     Each region loads through the factory, which reports the shape's failure after the shape's own error. A
///     missing required attribute is refused for every region in FactorySetSpec. Here each one is present and does not
///     read.
/// </remarks>
[TestFixture]
public class RegionLoadingSpec
{
    /// <summary>Three corners of a footprint, the fewest the factory takes, written as Corner children.</summary>
    private const string FootprintCorners = "    <Corner Position=\"(0, 0)\"/>\n" +
                                            "    <Corner Position=\"(10, 0)\"/>\n" +
                                            "    <Corner Position=\"(0, 10)\"/>\n";

    /// <summary>Three corners of a footprint, the last with a Position that does not read.</summary>
    private const string FootprintCornersWithOneUnreadable = "    <Corner Position=\"(0, 0)\"/>\n" +
                                                             "    <Corner Position=\"(10, 0)\"/>\n" +
                                                             "    <Corner Position=\"corner\"/>\n";

    /// <summary>A box's six planes, 1 from its middle each way, the last with a Distance that does not read.</summary>
    private const string HullPlanesWithOneUnreadable = "    <Plane Normal=\"(1, 0, 0)\" Distance=\"1\"/>\n" +
                                                       "    <Plane Normal=\"(-1, 0, 0)\" Distance=\"1\"/>\n" +
                                                       "    <Plane Normal=\"(0, 1, 0)\" Distance=\"1\"/>\n" +
                                                       "    <Plane Normal=\"(0, -1, 0)\" Distance=\"1\"/>\n" +
                                                       "    <Plane Normal=\"(0, 0, 1)\" Distance=\"1\"/>\n" +
                                                       "    <Plane Normal=\"(0, 0, -1)\" Distance=\"far\"/>\n";

    /// <summary>An OriginTarget child of a type no factory registers.</summary>
    private const string UnregisteredOriginTarget = "    <OriginTarget Type=\"Nowhere\"/>\n";

    [Test]
    public void RefusesEachRegionWithOneValueUnreadable()
    {
        // One case for each region type, each element loadable but for one value that does not read. The value is an
        // Origin or a Rotation, which every shape reads, one of the shape's own, a Corner's Position or a Plane's
        // Distance. The footprints and the hull have as many corners and planes as the factory takes, so only the value
        // refuses them. Three more cases give a circle, a cylinder and a sphere an OriginTarget of a type no factory
        // registers. Each case names where its first error stands and what that error is about.
        (string typeName, string attributes, string children, string refusal)[] cases =
        {
            ("Box", " Extents=\"(5, 5, 5)\" Rotation=\"sideways\"", "", "Line 1, the Region element's Rotation"),
            ("Capsule", " Radius=\"wide\" HalfHeight=\"5\"", "", "Line 1, the Region element's Radius"),
            ("Circle", " Origin=\"here\" Radius=\"5\"", "", "Line 1, the Region element's Origin"),
            ("ConvexHull", "", HullPlanesWithOneUnreadable, "Line 7, the Plane element's Distance"),
            ("Cylinder", " Radius=\"5\" HalfHeight=\"tall\"", "", "Line 1, the Region element's HalfHeight"),
            ("ExtrudedPolygon", " HalfHeight=\"tall\"", FootprintCorners, "Line 1, the Region element's HalfHeight"),
            ("Polygon", "", FootprintCornersWithOneUnreadable, "Line 4, the Corner element's Position"),
            ("Rectangle", " Extents=\"wide\"", "", "Line 1, the Region element's Extents"),
            ("Sector", " Angle=\"wide\" Range=\"20\" HalfHeight=\"1.5\"", "", "Line 1, the Region element's Angle"),
            ("Sphere", " Radius=\"wide\"", "", "Line 1, the Region element's Radius"),
            ("Circle", " Radius=\"5\"", UnregisteredOriginTarget, "Line 2, the OriginTarget element's type Nowhere"),
            ("Cylinder", " Radius=\"5\" HalfHeight=\"1.5\"", UnregisteredOriginTarget,
                "Line 2, the OriginTarget element's type Nowhere"),
            ("Sphere", " Radius=\"5\"", UnregisteredOriginTarget, "Line 2, the OriginTarget element's type Nowhere"),
        };

        Assert.Multiple(() =>
        {
            foreach ((string typeName, string attributes, string children, string refusal) in cases)
            {
                string snippet = children.Length == 0
                    ? $"<Region Type=\"{typeName}\"{attributes}/>"
                    : $"<Region Type=\"{typeName}\"{attributes}>\n{children}</Region>";
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

                Assert.That(wasCreated, Is.False, snippet);
                Assert.That(loaded, Is.Null, snippet);
                Assert.That(logSink.Entries, Has.Count.EqualTo(2), snippet);
                Assert.That(logSink.Entries.FirstOrDefault(), Does.StartWith($"Error guard.xml {refusal} "), snippet);
                Assert.That(logSink.Entries.LastOrDefault(),
                    Is.EqualTo($"Error guard.xml Line 1, the Region element's type {typeName} failed to initialize"),
                    snippet);
            }
        });
    }
}
