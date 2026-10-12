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
    public void ContainsEveryPointOnItsEdgesFromItsBottomToItsTop()
    {
        float[] bottomPlaneAndTop = { 0f, 1.5f, 3f };

        Assert.Multiple(() =>
        {
            foreach (float height in bottomPlaneAndTop)
            {
                foreach (Vector2 boundaryPoint in RegionSpecHelpers.LShapedFootprintBoundaryPoints)
                {
                    Vector3 onTheBoundary = new(boundaryPoint.X, height, boundaryPoint.Y);

                    Assert.That(_extrudedPolygon.IsInRegion(onTheBoundary), Is.True,
                        $"{onTheBoundary} is on the boundary");
                }
            }
        });
    }

    [Test]
    public void ContainsItsSurfaceFarFromTheWorldsOriginOnceTurned()
    {
        // The L's twelve boundary points at its bottom, its plane and its top, and three points inside the L on its top
        // and its bottom.
        float[] bottomPlaneAndTop = { -1.5f, 0f, 1.5f };
        List<Vector3> onTheBoundary = new()
        {
            new Vector3(2f, 1.5f, 2f), new Vector3(7.5f, 1.5f, 2.5f), new Vector3(2.5f, 1.5f, 7.5f),
            new Vector3(2f, -1.5f, 2f), new Vector3(7.5f, -1.5f, 2.5f), new Vector3(2.5f, -1.5f, 7.5f),
        };

        foreach (float height in bottomPlaneAndTop)
        {
            onTheBoundary.AddRange(RegionSpecHelpers.LShapedFootprintBoundaryPoints.Select(point =>
                new Vector3(point.X, height, point.Y)));
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_extrudedPolygon, onTheBoundary);
    }

    [Test]
    public void HoldsItsTopBottomAndEdgesWithinAMillimeter()
    {
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);
        Vector3 right = new(1f, 0f, 0f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_extrudedPolygon, new Vector3(2f, 1.5f, 2f), up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_extrudedPolygon, new Vector3(2f, -1.5f, 2f),
                down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_extrudedPolygon, new Vector3(10f, 0f, 2.5f),
                right);
        });
    }

    [Test]
    public void HoldsAPositionExactlyTheToleranceAboveItsTop()
    {
        // At the world's origin and unturned, a position comes into the shape's own coordinates unchanged, so this
        // one's height, over a point inside the L, is exactly the sum the shape compares with, its half height and the
        // tolerance added in float, which only an inclusive comparison holds.
        Vector3 exactlyTheToleranceAbove = new(2f, 1.5f + 0.001f, 2f);

        _extrudedPolygon.Origin = Vector3.Zero;

        Assert.That(_extrudedPolygon.IsInRegion(exactlyTheToleranceAbove), Is.True);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsEndsWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        Vector3 onTheTop = new(2f, 1.5f, 2f);
        Vector3 onTheBottom = new(2f, -1.5f, 2f);
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);

        _extrudedPolygon.Rotation = RegionSpecHelpers.AwkwardTurn;
        _extrudedPolygon.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_extrudedPolygon, onTheTop, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_extrudedPolygon, onTheBottom, down);
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

    [Test]
    public void ReadsANegativeHalfHeightAsItsSize()
    {
        Vector3 lowerLeftQuarterBelowThePlane = new(2f, 1f, 2f);

        _extrudedPolygon.HalfHeight = -1.5f;

        Assert.Multiple(() =>
        {
            Assert.That(_extrudedPolygon.IsInRegion(lowerLeftQuarterBelowThePlane), Is.True,
                "a HalfHeight of -1.5 stored as given holds nothing");
            Assert.That(_extrudedPolygon.HalfHeight, Is.EqualTo(1.5f));
        });
    }
}
