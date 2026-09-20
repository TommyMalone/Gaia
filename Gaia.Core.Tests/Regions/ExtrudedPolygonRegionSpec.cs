using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="ExtrudedPolygonRegion" />.</summary>
/// <remarks>
///     The footprint is an L, since a rectangular footprint would not tell <see cref="PolygonRegionBase" />'s
///     crossing test from a bounding box. It is a 10 by 10 square with its upper right quarter missing. Its plane is
///     raised to 1.5, so that a half-height of 1.5 puts the floor on the ground.
/// </remarks>
[TestFixture]
public class ExtrudedPolygonRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);

        _extrudedPolygon = new ExtrudedPolygonRegion
        {
            Origin = new Vector3(0f, 1.5f, 0f),
            HalfHeight = 1.5f,
            RandomSource = _randomSource,
        };
        RegionSpecHelpers.AddLShapedFootprint(_extrudedPolygon);
    }

    private Xoshiro128RandomSource _randomSource;

    private ExtrudedPolygonRegion _extrudedPolygon;

    [Test]
    public void ContainsItsFootprintOnlyWithinItsHalfHeight()
    {
        Vector3 lowerLeftQuarter = new(2f, 1f, 2f);
        Vector3 onTop = new(2f, 3f, 2f);
        Vector3 aboveTheTop = new(2f, 4f, 2f);
        Vector3 belowTheBottom = new(2f, -1f, 2f);
        Vector3 missingQuarter = new(8f, 1f, 8f);

        Assert.Multiple(() =>
        {
            Assert.That(_extrudedPolygon.IsInRegion(lowerLeftQuarter), Is.True);
            Assert.That(_extrudedPolygon.IsInRegion(onTop), Is.True, "the top counts as inside");
            Assert.That(_extrudedPolygon.IsInRegion(aboveTheTop), Is.False);
            Assert.That(_extrudedPolygon.IsInRegion(belowTheBottom), Is.False);
            Assert.That(_extrudedPolygon.IsInRegion(missingQuarter), Is.False, "the missing quarter is still missing");
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_extrudedPolygon);
    }

    [Test]
    public void SpreadsSamplesAlongItsHeight()
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
    public void TurnsAboutItsOriginWithItsRotation()
    {
        // A quarter turn to the right lays the L along the world's X, between Z of 0 and -10, at the same height.
        Vector3 lowerRightQuarterOnceTurned = new(2f, 1f, -8f);
        Vector3 lowerLeftQuarterBeforeTurning = new(2f, 1f, 2f);

        _extrudedPolygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_extrudedPolygon.IsInRegion(lowerRightQuarterOnceTurned), Is.True);
            Assert.That(_extrudedPolygon.IsInRegion(lowerLeftQuarterBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _extrudedPolygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_extrudedPolygon);
    }

    [Test]
    public void SamplesNothingWithFewerThanThreeCorners()
    {
        Vector2 lineStart = new(0f, 0f);
        Vector2 lineEnd = new(10f, 0f);

        ExtrudedPolygonRegion line = new()
        {
            HalfHeight = 1.5f,
            RandomSource = _randomSource,
        };
        line.AddCorner(lineStart);
        line.AddCorner(lineEnd);

        Assert.That(line.TryGetRandomPosition(out _), Is.False);
    }
}
