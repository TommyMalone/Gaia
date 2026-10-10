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
