using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="RectangleRegion" />, and the frame loading every planar region shares.</summary>
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
            Min = new Vector2(0f, 0f),
            Max = new Vector2(10f, 20f),
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private RectangleRegion _rectangle;

    [Test]
    public void ContainsWhatIsBetweenItsCorners()
    {
        Vector3 middle = new(5f, 0f, 10f);
        Vector3 nearCorner = new(0f, 0f, 0f);
        Vector3 farCorner = new(10f, 0f, 20f);
        Vector3 pastTheNearSide = new(-1f, 0f, 10f);
        Vector3 pastTheFarSide = new(5f, 0f, 21f);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(middle), Is.True);
            Assert.That(_rectangle.IsInRegion(nearCorner), Is.True, "the corner counts as inside");
            Assert.That(_rectangle.IsInRegion(farCorner), Is.True, "the far corner counts as inside");
            Assert.That(_rectangle.IsInRegion(pastTheNearSide), Is.False);
            Assert.That(_rectangle.IsInRegion(pastTheFarSide), Is.False);
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveMiddle = new(5f, 100f, 10f);

        Assert.That(_rectangle.IsInRegion(farAboveMiddle), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_rectangle);
    }

    [Test]
    public void MeasuresItsCornersFromItsOrigin()
    {
        Vector3 middleFromTheOrigin = new(105f, 0f, 110f);
        Vector3 middleOfTheWorld = new(5f, 0f, 10f);

        _rectangle.Origin = new Vector3(100f, 7f, 100f);
        _rectangle.TryGetRandomPosition(out Vector3 sample);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(middleFromTheOrigin), Is.True);
            Assert.That(_rectangle.IsInRegion(middleOfTheWorld), Is.False);
            Assert.That(sample.Y, Is.EqualTo(7f), "a sample lies in the rectangle's plane, through Origin");
        });
    }

    [Test]
    public void TurnsAboutItsOriginWithItsRotation()
    {
        // A quarter turn to the right swings the long side from the world's Z onto its X, so the rectangle now runs
        // from Z of 0 back to -10.
        Vector3 insideOnceTurned = new(15f, 0f, -5f);
        Vector3 middleBeforeTurning = new(5f, 0f, 10f);

        _rectangle.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(insideOnceTurned), Is.True);
            Assert.That(_rectangle.IsInRegion(middleBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _rectangle.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_rectangle);
    }

    [Test]
    public void LoadsItsFrameWithItsCorners()
    {
        const string snippet = """
                               <Region Type="Rectangle" Origin="(100, 0, 100)" Rotation="(0, 90, 0)"
                                       Min="(0, 0)" Max="(10, 20)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RectangleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Origin, Is.EqualTo(new Vector3(100f, 0f, 100f)));
            Assert.That(loaded.Max, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(loaded.IsInRegion(new Vector3(115f, 0f, 95f)), Is.True, "turned a quarter turn about Origin");
            Assert.That(loaded.IsInRegion(new Vector3(105f, 0f, 110f)), Is.False, "where it would lie unturned");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LiesOnTheGroundAtTheWorldsOriginWithoutAFrame()
    {
        const string snippet = """
                               <Region Type="Rectangle" Min="(0, 0)" Max="(10, 20)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RectangleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Origin, Is.EqualTo(Vector3.Zero));
            Assert.That(loaded.Rotation, Is.EqualTo(Rotation.Identity));
            Assert.That(loaded.IsInRegion(new Vector3(5f, 0f, 10f)), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }
}
