using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="ExtrudedPolygonRegion" />.</summary>
/// <remarks>
///     The footprint is an L, a 10 by 10 square with its upper right quarter missing, because a convex footprint
///     would not tell a crossing test from a bounding box.
/// </remarks>
[TestFixture]
public class ExtrudedPolygonRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _extrudedPolygon = new ExtrudedPolygonRegion
        {
            Floor = 0f,
            Ceiling = 3f,
            RandomSource = _randomSource,
        };
        RegionSpecHelpers.AddLShapedFootprint(_extrudedPolygon);
    }

    private SystemRandomSource _randomSource;

    private ExtrudedPolygonRegion _extrudedPolygon;

    [Test]
    public void ContainsItsFootprintOnlyBetweenFloorAndCeiling()
    {
        Vector3 lowerLeftQuarter = new(2f, 1f, 2f);
        Vector3 onCeiling = new(2f, 3f, 2f);
        Vector3 aboveCeiling = new(2f, 4f, 2f);
        Vector3 belowFloor = new(2f, -1f, 2f);
        Vector3 missingQuarter = new(8f, 1f, 8f);

        Assert.Multiple(() =>
        {
            Assert.That(_extrudedPolygon.IsInRegion(lowerLeftQuarter), Is.True);
            Assert.That(_extrudedPolygon.IsInRegion(onCeiling), Is.True, "the ceiling counts as inside");
            Assert.That(_extrudedPolygon.IsInRegion(aboveCeiling), Is.False);
            Assert.That(_extrudedPolygon.IsInRegion(belowFloor), Is.False);
            Assert.That(_extrudedPolygon.IsInRegion(missingQuarter), Is.False, "the missing quarter is still missing");
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_extrudedPolygon);
    }

    [Test]
    public void SpreadsSamplesBetweenItsFloorAndCeiling()
    {
        bool sawLowerHalf = false;
        bool sawUpperHalf = false;

        for (int sampleIndex = 0; sampleIndex < RegionSpecHelpers.SampleCount; sampleIndex++)
        {
            _extrudedPolygon.TryGetRandomPosition(out Vector3 position);

            sawLowerHalf |= position.Y < 1.5f;
            sawUpperHalf |= position.Y >= 1.5f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawLowerHalf, Is.True);
            Assert.That(sawUpperHalf, Is.True);
        });
    }

    [Test]
    public void SamplesNothingWithFewerThanThreeCorners()
    {
        Vector3 lineStart = new(0f, 0f, 0f);
        Vector3 lineEnd = new(10f, 0f, 0f);

        ExtrudedPolygonRegion line = new()
        {
            Floor = 0f,
            Ceiling = 3f,
            RandomSource = _randomSource,
        };
        line.AddCorner(lineStart);
        line.AddCorner(lineEnd);

        Assert.That(line.TryGetRandomPosition(out _), Is.False);
    }
}
