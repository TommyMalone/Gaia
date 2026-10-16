using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="BoxRegion" />.</summary>
[TestFixture]
public class BoxRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        _box = new BoxRegion
        {
            Origin = new Vector3(5f, 1.5f, 10f),
            Extents = new Vector3(5f, 1.5f, 10f),
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private BoxRegion _box;

    [Test]
    public void ContainsWhatIsWithinItsExtents()
    {
        Vector3 middle = new(5f, 1f, 10f);
        Vector3 onCeiling = new(5f, 3f, 10f);
        Vector3 aboveCeiling = new(5f, 4f, 10f);
        Vector3 belowFloor = new(5f, -1f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_box.IsInRegion(middle), Is.True);
            Assert.That(_box.IsInRegion(onCeiling), Is.True, "the ceiling counts as inside");
            Assert.That(_box.IsInRegion(aboveCeiling), Is.False);
            Assert.That(_box.IsInRegion(belowFloor), Is.False);
        });
    }

    [Test]
    public void ContainsItsFacesFarFromTheWorldsOriginOnceTurned()
    {
        // The box's eight corners, the middle of each of its six faces, and three points elsewhere on its faces.
        Vector3[] onTheBoundary =
        {
            new(-5f, -1.5f, -10f), new(5f, -1.5f, -10f), new(-5f, 1.5f, -10f), new(5f, 1.5f, -10f),
            new(-5f, -1.5f, 10f), new(5f, -1.5f, 10f), new(-5f, 1.5f, 10f), new(5f, 1.5f, 10f),
            new(-5f, 0f, 0f), new(5f, 0f, 0f), new(0f, -1.5f, 0f), new(0f, 1.5f, 0f), new(0f, 0f, -10f),
            new(0f, 0f, 10f), new(5f, 0.7f, -3.3f), new(-2.2f, 1.5f, 6.1f), new(1.9f, -0.4f, -10f),
        };

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_box, onTheBoundary);
    }

    [Test]
    public void HoldsItsFacesWithinAMillimeter()
    {
        Vector3[] outwardFromEachFace =
        {
            new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 1f, 0f), new(0f, -1f, 0f), new(0f, 0f, 1f), new(0f, 0f, -1f),
        };

        Assert.Multiple(() =>
        {
            foreach (Vector3 outward in outwardFromEachFace)
            {
                Vector3 middleOfTheFace = new(outward.X * 5f, outward.Y * 1.5f, outward.Z * 10f);

                RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_box, middleOfTheFace, outward);
            }
        });
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    [TestCase(-2f, 0.5f, 8f)]
    public void HoldsItsFacesWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // A negative scale mirrors the box, and the tolerance along that axis is divided by the scale's size.
        Vector3[] outwardFromEachFace =
        {
            new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 1f, 0f), new(0f, -1f, 0f), new(0f, 0f, 1f), new(0f, 0f, -1f),
        };

        _box.Rotation = RegionSpecHelpers.AwkwardTurn;
        _box.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            foreach (Vector3 outward in outwardFromEachFace)
            {
                Vector3 middleOfTheFace = new(outward.X * 5f, outward.Y * 1.5f, outward.Z * 10f);

                RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_box, middleOfTheFace, outward);
            }
        });
    }

    [TestCase(5f + 0.001f, 0f, 0f)]
    [TestCase(0f, 1.5f + 0.001f, 0f)]
    [TestCase(0f, 0f, 10f + 0.001f)]
    public void HoldsAPositionExactlyTheTolerancePastAFace(float x, float y, float z)
    {
        // At the world's origin and unturned, a position comes into the box's own coordinates unchanged, so this one's
        // coordinate on its axis is exactly the sum the box compares with, its extent and the tolerance added in float,
        // which only an inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(x, y, z);

        _box.Origin = Vector3.Zero;

        Assert.That(_box.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_box);
    }

    [Test]
    public void SamplesBelowItsOriginAlongEveryAxis()
    {
        // A sampler that guessed from zero to Extents would still land every sample inside the box, in the eighth of it
        // past Origin, so the test asks for a sample below Origin on each axis. The chance that fifty uniform samples
        // all lie past Origin on one axis is one in 2 to the 50th.
        bool sawBelowOriginAlongX = false;
        bool sawBelowOriginAlongY = false;
        bool sawBelowOriginAlongZ = false;

        for (int sampleIndex = 0; sampleIndex < RegionSpecHelpers.SampleCount; sampleIndex++)
        {
            _box.TryGetRandomPosition(out Vector3 position);

            sawBelowOriginAlongX |= position.X < 5f;
            sawBelowOriginAlongY |= position.Y < 1.5f;
            sawBelowOriginAlongZ |= position.Z < 10f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawBelowOriginAlongX, Is.True);
            Assert.That(sawBelowOriginAlongY, Is.True);
            Assert.That(sawBelowOriginAlongZ, Is.True);
        });
    }

    [Test]
    public void ReachesItsExtentsOutFromItsCenter()
    {
        Vector3 center = new(5f, 1f, 10f);
        Vector3 extents = new(5f, 2f, 10f);

        BoxRegion box = new(center, extents);

        Assert.Multiple(() =>
        {
            Assert.That(box.Origin, Is.EqualTo(center));
            Assert.That(box.Extents, Is.EqualTo(extents));
            Assert.That(box.IsInRegion(center), Is.True);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _box.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_box);
    }

    [Test]
    public void LoadsItsOriginRotationAndExtents()
    {
        // A box 4 along its right, 2 along its up and 1 along its forward, centered at (12, 1, 4) and turned 30
        // degrees right. The position is 1.84 along its right and 0.02 along its forward from its middle, so the box
        // holds it only once Origin, Rotation and Extents are all read. The snippet loads through the factory, which
        // refuses an attribute the description does not name, so the description is held as well as the reads.
        const string snippet = """
                               <Region Type="Box" Origin="(12, 1, 4)" Rotation="(0, 30, 0)" Extents="(2, 1, 0.5)"/>
                               """;
        Vector3 alongTheTurnedRight = new(13.6f, 1f, 3.1f);
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loaded?.IsInRegion(alongTheTurnedRight), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesTheCornersItNoLongerTakes()
    {
        const string snippet = """<Region Type="Box" Extents="(5, 5, 5)" Min="(0, 0, 0)"/>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the attribute before Init runs, so the test loads through the factory rather than
        // calling Init.
        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element does not take the Min attribute",
            }));
        });
    }

    [Test]
    public void RefusesAZeroScale()
    {
        const string snippet = """<Region Type="Box" Scale="(0, 1, 1)" Extents="(1, 1, 1)"/>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        BoxRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element's Scale attribute has a zero component, which leaves the shape containing nothing",
            }));
        });
    }

    [Test]
    public void ReadsANegativeExtentAsItsSize()
    {
        Vector3 withinTheFlippedExtent = new(0.9f, 0f, 0f);
        Vector3 flippedExtents = new(1f, 1f, 1f);

        BoxRegion box = new() { Extents = new Vector3(-1f, 1f, 1f) };

        Assert.Multiple(() =>
        {
            Assert.That(box.IsInRegion(withinTheFlippedExtent), Is.True,
                "an extent of -1 stored as given holds nothing");
            Assert.That(box.Extents, Is.EqualTo(flippedExtents));
        });
    }
}
