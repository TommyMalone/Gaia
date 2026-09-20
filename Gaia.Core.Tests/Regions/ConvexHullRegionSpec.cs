using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="ConvexHullRegion" />.</summary>
/// <remarks>
///     The hulls under test are a box, whose answers <see cref="BoxRegion" /> makes obvious, and a wedge, because the
///     interesting failure is a normal pointing the wrong way and a symmetric shape hides it.
/// </remarks>
[TestFixture]
public class ConvexHullRegionSpec
{
    [SetUp]
    public void BuildRegions()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        Plane leftWall = new(TowardNegativeX, 0f);
        Plane rightWall = new(TowardPositiveX, 10f);
        Plane floor = new(TowardNegativeY, 0f);
        Plane ceiling = new(TowardPositiveY, 3f);
        Plane nearWall = new(TowardNegativeZ, 0f);
        Plane farWall = new(TowardPositiveZ, 20f);

        // A box from the origin to (10, 3, 20), as six outward-facing planes.
        _hullBox = new ConvexHullRegion { RandomSource = _randomSource };
        _hullBox.AddPlane(leftWall);
        _hullBox.AddPlane(rightWall);
        _hullBox.AddPlane(floor);
        _hullBox.AddPlane(ceiling);
        _hullBox.AddPlane(nearWall);
        _hullBox.AddPlane(farWall);

        // A wedge, the box above cut by the diagonal plane x + z <= 10, keeping the corner at the origin. The right
        // and far walls are left out, since the diagonal with the near and left walls already caps x and z at 10.
        Vector3 pointOnDiagonal = new(10f, 0f, 0f);
        Plane diagonal = Plane.FromPointAndNormal(pointOnDiagonal, DiagonalNormal);

        _hullWedge = new ConvexHullRegion { RandomSource = _randomSource };
        _hullWedge.AddPlane(leftWall);
        _hullWedge.AddPlane(floor);
        _hullWedge.AddPlane(ceiling);
        _hullWedge.AddPlane(nearWall);
        _hullWedge.AddPlane(diagonal);
    }

    private static readonly Vector3 TowardNegativeX = new(-1f, 0f, 0f);
    private static readonly Vector3 TowardPositiveX = new(1f, 0f, 0f);
    private static readonly Vector3 TowardNegativeY = new(0f, -1f, 0f);
    private static readonly Vector3 TowardPositiveY = new(0f, 1f, 0f);
    private static readonly Vector3 TowardNegativeZ = new(0f, 0f, -1f);
    private static readonly Vector3 TowardPositiveZ = new(0f, 0f, 1f);
    private static readonly Vector3 DiagonalNormal = new(1f, 0f, 1f);

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private ConvexHullRegion _hullBox;
    private ConvexHullRegion _hullWedge;

    [Test]
    public void ContainsWhatIsInsideABoxShapedHull()
    {
        Vector3 middle = new(5f, 1f, 10f);
        Vector3 farCorner = new(10f, 3f, 20f);
        Vector3 pastTheRightWall = new(11f, 1f, 10f);
        Vector3 aboveCeiling = new(5f, 4f, 10f);
        Vector3 pastTheNearWall = new(5f, 1f, -1f);

        Assert.Multiple(() =>
        {
            Assert.That(_hullBox.IsInRegion(middle), Is.True);
            Assert.That(_hullBox.IsInRegion(farCorner), Is.True, "the far corner counts as inside");
            Assert.That(_hullBox.IsInRegion(pastTheRightWall), Is.False);
            Assert.That(_hullBox.IsInRegion(aboveCeiling), Is.False);
            Assert.That(_hullBox.IsInRegion(pastTheNearWall), Is.False);
        });
    }

    [Test]
    public void ContainsWhatIsInsideAWedge()
    {
        // The corner near the origin is kept and the corner away from it is cut off.
        Vector3 nearTheOrigin = new(1f, 1f, 1f);
        Vector3 justShortOfTheDiagonal = new(4f, 1f, 5f);
        Vector3 cutOffCorner = new(9f, 1f, 9f);
        Vector3 pastTheLeftWall = new(-1f, 1f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(_hullWedge.IsInRegion(nearTheOrigin), Is.True);
            Assert.That(_hullWedge.IsInRegion(justShortOfTheDiagonal), Is.True, "just short of the diagonal is inside");
            Assert.That(_hullWedge.IsInRegion(cutOffCorner), Is.False, "the far corner was cut off");
            Assert.That(_hullWedge.IsInRegion(pastTheLeftWall), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_hullBox);
        RegionSpecHelpers.AssertEverySampleLandsInside(_hullWedge);
    }

    [Test]
    public void ContainsNothingWithNoPlanes()
    {
        // No planes would otherwise mean everywhere, and a region that contains the whole world is never what a
        // configuration meant.
        ConvexHullRegion empty = new() { RandomSource = _randomSource };

        Assert.Multiple(() =>
        {
            Assert.That(empty.IsInRegion(Vector3.Zero), Is.False);
            Assert.That(empty.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void WorksOutItsBoundsFromWhereItsPlanesMeet()
    {
        Vector3 origin = new(0f, 0f, 0f);
        Vector3 boxFarCorner = new(10f, 3f, 20f);
        Vector3 wedgeFarCorner = new(10f, 3f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_hullBox.TryGetBounds(out Vector3 boxMin, out Vector3 boxMax), Is.True);
            RegionSpecHelpers.AssertNearly(boxMin, origin);
            RegionSpecHelpers.AssertNearly(boxMax, boxFarCorner);

            // The diagonal plane's normal is not a round number, so the corners it produces are not either.
            Assert.That(_hullWedge.TryGetBounds(out Vector3 wedgeMin, out Vector3 wedgeMax), Is.True);
            RegionSpecHelpers.AssertNearly(wedgeMin, origin);
            RegionSpecHelpers.AssertNearly(wedgeMax, wedgeFarCorner);
        });
    }

    [Test]
    public void HasNoBoundsWhenItsPlanesDoNotEncloseAnything()
    {
        // A slab between two parallel planes goes on forever, so there is nowhere to sample from, and the region has
        // to say so rather than hand back a position outside itself.
        Plane upperFace = new(TowardPositiveX, 8f);
        Plane lowerFace = new(TowardNegativeX, -2f);
        Vector3 farFromTheOriginInsideTheSlab = new(5f, 100f, -100f);

        ConvexHullRegion slab = new() { RandomSource = _randomSource };
        slab.AddPlane(upperFace);
        slab.AddPlane(lowerFace);

        Assert.Multiple(() =>
        {
            Assert.That(slab.IsInRegion(farFromTheOriginInsideTheSlab), Is.True, "the slab is unbounded in Y and Z");
            Assert.That(slab.TryGetBounds(out _, out _), Is.False);
            Assert.That(slab.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void HasNoBoundsWhenItsPlanesLeaveAnOpenEnd()
    {
        // Three planes around the Y axis make a triangular tube with no top or bottom. Three planes are fewer than the
        // four a closed volume needs, so TryGetBounds reports no bounds before the region looks for corners.
        Plane leftWall = new(TowardNegativeX, 0f);
        Plane nearWall = new(TowardNegativeZ, 0f);
        Plane diagonal = new(DiagonalNormal, 10f);

        ConvexHullRegion tube = new() { RandomSource = _randomSource };
        tube.AddPlane(leftWall);
        tube.AddPlane(nearWall);
        tube.AddPlane(diagonal);

        Assert.That(tube.TryGetBounds(out _, out _), Is.False);
    }

    [Test]
    public void LoadsItsPlanesFromPlaneElements()
    {
        const string snippet = """
                               <Region Type="ConvexHull">
                                   <Plane Normal="(1, 0, 0)" Distance="1"/>
                                   <Plane Normal="(-1, 0, 0)" Distance="1"/>
                                   <Plane Normal="(0, 1, 0)" Distance="1"/>
                                   <Plane Normal="(0, -1, 0)" Distance="1"/>
                                   <Plane Normal="(0, 0, 1)" Distance="1"/>
                                   <Plane Normal="(0, 0, -1)" Distance="1"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConvexHullRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Planes, Has.Count.EqualTo(6));
            Assert.That(loaded.IsInRegion(Vector3.Zero), Is.True);
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 0f)), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAHullWithTooFewPlanes()
    {
        const string snippet = """
                               <Region Type="ConvexHull">
                                   <Plane Normal="(0, 1, 0)" Distance="1"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConvexHullRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has fewer than 4 Plane elements, too few to close a volume",
            }));
        });
    }

    [Test]
    public void RefusesAPlaneWhoseNormalHasNoLength()
    {
        const string snippet = """
                               <Region Type="ConvexHull">
                                   <Plane Normal="(0, 0, 0)" Distance="1"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConvexHullRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Plane element's Normal has no length",
            }));
        });
    }
}
