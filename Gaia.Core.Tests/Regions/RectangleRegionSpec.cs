using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="RectangleRegion" />, and the Origin and Rotation loading every shape shares.</summary>
[TestFixture]
public class RectangleRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        _rectangle = new RectangleRegion
        {
            Extents = new Vector2(5f, 10f),
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private RectangleRegion _rectangle;

    [Test]
    public void ContainsWhatIsWithinItsExtents()
    {
        Vector3 middle = new(0f, 0f, 0f);
        Vector3 nearCorner = new(-5f, 0f, -10f);
        Vector3 farCorner = new(5f, 0f, 10f);
        Vector3 pastTheLeftSide = new(-6f, 0f, 0f);
        Vector3 pastTheNearSide = new(0f, 0f, -11f);
        Vector3 pastTheFarSide = new(0f, 0f, 11f);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(middle), Is.True);
            Assert.That(_rectangle.IsInRegion(nearCorner), Is.True, "the corner counts as inside");
            Assert.That(_rectangle.IsInRegion(farCorner), Is.True, "the far corner counts as inside");
            Assert.That(_rectangle.IsInRegion(pastTheLeftSide), Is.False);
            Assert.That(_rectangle.IsInRegion(pastTheNearSide), Is.False);
            Assert.That(_rectangle.IsInRegion(pastTheFarSide), Is.False);
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveMiddle = new(0f, 100f, 0f);

        Assert.That(_rectangle.IsInRegion(farAboveMiddle), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_rectangle);
    }

    [Test]
    public void SamplesBelowItsOriginAlongBothAxes()
    {
        // A sampler that guessed from zero to Extents would still land every sample inside the rectangle, in the
        // quarter of it past Origin, so the test asks for a sample below Origin on each axis. The chance that fifty
        // uniform samples all lie past Origin on one axis is one in 2 to the 50th.
        bool sawBelowOriginAlongX = false;
        bool sawBelowOriginAlongZ = false;

        for (int sampleIndex = 0; sampleIndex < RegionSpecHelpers.SampleCount; sampleIndex++)
        {
            _rectangle.TryGetRandomPosition(out Vector3 position);

            sawBelowOriginAlongX |= position.X < 0f;
            sawBelowOriginAlongZ |= position.Z < 0f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawBelowOriginAlongX, Is.True);
            Assert.That(sawBelowOriginAlongZ, Is.True);
        });
    }

    [Test]
    public void ReachesItsExtentsOutFromItsCenter()
    {
        Vector3 center = new(100f, 0f, 50f);
        Vector2 extents = new(5f, 10f);

        RectangleRegion rectangle = new(center, extents);

        Assert.Multiple(() =>
        {
            Assert.That(rectangle.Origin, Is.EqualTo(center));
            Assert.That(rectangle.Extents, Is.EqualTo(extents));
            Assert.That(rectangle.IsInRegion(center), Is.True);
        });
    }

    [Test]
    public void MeasuresItsExtentsFromItsOrigin()
    {
        Vector3 rightOfAndBehindTheMovedMiddle = new(104f, 0f, 91f);
        Vector3 middleOfTheWorld = new(0f, 0f, 0f);

        _rectangle.Origin = new Vector3(100f, 7f, 100f);
        _rectangle.TryGetRandomPosition(out Vector3 sample);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(rightOfAndBehindTheMovedMiddle), Is.True);
            Assert.That(_rectangle.IsInRegion(middleOfTheWorld), Is.False);
            Assert.That(sample.Y, Is.EqualTo(7f), "a sample lies in the rectangle's plane, through Origin");
        });
    }

    [Test]
    public void TurnsAboutItsOriginWithItsRotation()
    {
        // A quarter turn to the right swings the long side from the world's Z onto its X, so the rectangle now runs
        // from X of -10 to 10 and Z of -5 to 5.
        Vector3 insideOnceTurned = new(8f, 0f, 0f);
        Vector3 insideBeforeTurning = new(0f, 0f, 8f);

        _rectangle.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(insideOnceTurned), Is.True);
            Assert.That(_rectangle.IsInRegion(insideBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _rectangle.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_rectangle);
    }

    [Test]
    public void LoadsItsOriginRotationAndExtents()
    {
        const string snippet = """
                               <Region Type="Rectangle" Origin="(100, 0, 100)" Rotation="(0, 90, 0)" Extents="(5, 10)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RectangleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Origin, Is.EqualTo(new Vector3(100f, 0f, 100f)));
            Assert.That(loaded.Extents, Is.EqualTo(new Vector2(5f, 10f)));
            Assert.That(loaded.IsInRegion(new Vector3(108f, 0f, 100f)), Is.True, "turned a quarter turn about Origin");
            Assert.That(loaded.IsInRegion(new Vector3(100f, 0f, 108f)), Is.False, "where it would lie unturned");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LiesOnTheGroundAtTheWorldsOriginByDefault()
    {
        const string snippet = """
                               <Region Type="Rectangle" Extents="(5, 10)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RectangleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Origin, Is.EqualTo(Vector3.Zero));
            Assert.That(loaded.Rotation, Is.EqualTo(Rotation.Identity));
            Assert.That(loaded.IsInRegion(new Vector3(4f, 0f, -9f)), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesTheCornersItNoLongerTakes()
    {
        const string snippet = """<Region Type="Rectangle" Extents="(5, 10)" Min="(0, 0)"/>""";
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
    public void FollowsItsOriginTarget()
    {
        Vector3 rightOfAndBehindTheFirstPosition = new(104f, 0f, 91f);
        Vector3 rightOfAndBehindTheSecondPosition = new(204f, 0f, 191f);
        Vector3 middleOfTheWorld = new(0f, 0f, 0f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };
        _rectangle.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(rightOfAndBehindTheFirstPosition), Is.True);
            Assert.That(_rectangle.IsInRegion(middleOfTheWorld), Is.False, "where it lies without the target");
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(_rectangle.IsInRegion(rightOfAndBehindTheSecondPosition), Is.True);
    }

    [Test]
    public void ReadsANegativeExtentAsItsSize()
    {
        Vector3 withinTheFlippedExtent = new(0.9f, 0f, 0f);
        Vector2 flippedExtents = new(1f, 2f);

        RectangleRegion rectangle = new() { Extents = new Vector2(-1f, -2f) };

        Assert.Multiple(() =>
        {
            Assert.That(rectangle.IsInRegion(withinTheFlippedExtent), Is.True,
                "an extent of -1 or -2 stored as given holds nothing");
            Assert.That(rectangle.Extents, Is.EqualTo(flippedExtents));
        });
    }
}
