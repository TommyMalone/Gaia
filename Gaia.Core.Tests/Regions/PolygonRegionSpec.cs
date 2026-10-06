using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="PolygonRegion" />.</summary>
/// <remarks>
///     The footprint is an L, since a rectangular footprint would not tell <see cref="PolygonRegionBase" />'s
///     crossing test from a bounding box. It is a 10 by 10 square with its upper right quarter missing.
/// </remarks>
[TestFixture]
public class PolygonRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _logSink = new RecordingLogSink();
        _randomSource = new Xoshiro128RandomSource(12345);

        _polygon = new PolygonRegion { RandomSource = _randomSource };
        RegionSpecHelpers.AddLShapedFootprint(_polygon);
    }

    private RecordingLogSink _logSink;

    private Xoshiro128RandomSource _randomSource;

    private PolygonRegion _polygon;

    [Test]
    public void ContainsWhatIsInsideItsFootprint()
    {
        Vector3 lowerLeftQuarter = new(2f, 0f, 2f);
        Vector3 lowerRightQuarter = new(8f, 0f, 2f);
        Vector3 upperLeftQuarter = new(2f, 0f, 8f);
        Vector3 missingQuarter = new(8f, 0f, 8f);
        Vector3 pastTheLeftSide = new(-1f, 0f, 2f);
        Vector3 pastTheTop = new(2f, 0f, 11f);

        Assert.Multiple(() =>
        {
            Assert.That(_polygon.IsInRegion(lowerLeftQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(lowerRightQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(upperLeftQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(missingQuarter), Is.False, "the missing quarter is outside the L");
            Assert.That(_polygon.IsInRegion(pastTheLeftSide), Is.False);
            Assert.That(_polygon.IsInRegion(pastTheTop), Is.False);
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveLowerLeftQuarter = new(2f, 100f, 2f);

        Assert.That(_polygon.IsInRegion(farAboveLowerLeftQuarter), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_polygon);
    }

    [Test]
    public void TurnsAboutItsOriginWithItsRotation()
    {
        // A quarter turn to the right lays the L along the world's X, between Z of 0 and -10, with the missing quarter
        // now at the far end.
        Vector3 lowerRightQuarterOnceTurned = new(2f, 0f, -8f);
        Vector3 missingQuarterOnceTurned = new(8f, 0f, -8f);
        Vector3 lowerLeftQuarterBeforeTurning = new(2f, 0f, 2f);

        _polygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_polygon.IsInRegion(lowerRightQuarterOnceTurned), Is.True);
            Assert.That(_polygon.IsInRegion(missingQuarterOnceTurned), Is.False, "the missing quarter turns with it");
            Assert.That(_polygon.IsInRegion(lowerLeftQuarterBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _polygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_polygon);
    }

    [Test]
    public void ContainsNothingWithFewerThanThreeCorners()
    {
        Vector2 lineStart = new(0f, 0f);
        Vector2 lineEnd = new(10f, 0f);
        Vector3 onTheLine = new(5f, 0f, 0f);

        PolygonRegion line = new() { RandomSource = _randomSource };
        line.AddCorner(lineStart);
        line.AddCorner(lineEnd);

        Assert.Multiple(() =>
        {
            Assert.That(line.IsInRegion(onTheLine), Is.False);
            Assert.That(line.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void LoadsItsCornersFromCornerElements()
    {
        const string snippet = """
                               <Region Type="Polygon">
                                   <Corner Position="(0, 0)"/>
                                   <Corner Position="(10, 0)"/>
                                   <Corner Position="(0, 10)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        PolygonRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Corners, Has.Count.EqualTo(3));
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 2f)), Is.True);
            Assert.That(loaded.IsInRegion(new Vector3(9f, 0f, 9f)), Is.False, "beyond the diagonal edge");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAFootprintWithoutArea()
    {
        const string snippet = """
                               <Region Type="Polygon">
                                   <Corner Position="(0, 0)"/>
                                   <Corner Position="(10, 0)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the short slot after Init returns true, so the test loads through the factory rather than
        // calling Init.
        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has fewer than 3 Corner elements",
            }));
        });
    }
}
