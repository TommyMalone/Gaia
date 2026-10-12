using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="CapsuleRegion" />.</summary>
[TestFixture]
public class CapsuleRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);

        _capsule = new CapsuleRegion
        {
            Origin = new Vector3(10f, 5f, 10f),
            HalfHeight = 5f,
            Radius = 2f,
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;

    private CapsuleRegion _capsule;

    [Test]
    public void ContainsWhatIsWithinReachOfItsSegment()
    {
        Vector3 middle = new(10f, 5f, 10f);
        Vector3 onSide = new(12f, 5f, 10f);
        Vector3 insideTheCap = new(11f, 9f, 10f);
        Vector3 justOutsideTheSide = new(13f, 5f, 10f);
        Vector3 boundingBoxCorner = new(12f, 9f, 12f);

        Assert.Multiple(() =>
        {
            Assert.That(_capsule.IsInRegion(middle), Is.True);
            Assert.That(_capsule.IsInRegion(onSide), Is.True, "the edge counts as inside");
            Assert.That(_capsule.IsInRegion(insideTheCap), Is.True, "the cap reaches above a flat top");
            Assert.That(_capsule.IsInRegion(justOutsideTheSide), Is.False);
            Assert.That(_capsule.IsInRegion(boundingBoxCorner), Is.False, "the corner of the bounding box is outside");
        });
    }

    [Test]
    public void ContainsNothingBeyondTheCaps()
    {
        Vector3 topOfTheUpperCap = new(10f, 10f, 10f);
        Vector3 aboveTheUpperCap = new(10f, 11f, 10f);
        Vector3 belowTheLowerCap = new(10f, -1f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_capsule.IsInRegion(topOfTheUpperCap), Is.True, "the tip of the cap counts as inside");
            Assert.That(_capsule.IsInRegion(aboveTheUpperCap), Is.False);
            Assert.That(_capsule.IsInRegion(belowTheLowerCap), Is.False);
        });
    }

    [Test]
    public void ContainsItsSurfaceFarFromTheWorldsOriginOnceTurned()
    {
        // The segment runs from 3 below the middle to 3 above it. Six points around the side at its bottom, its
        // middle and its top, the tip of each cap, and a point halfway down each cap.
        float halfwayDownACap = 2f / MathF.Sqrt(2f);
        List<Vector3> onTheBoundary = new()
        {
            new Vector3(0f, 5f, 0f), new Vector3(0f, -5f, 0f), new Vector3(halfwayDownACap, 3f + halfwayDownACap, 0f),
            new Vector3(0f, -3f - halfwayDownACap, -halfwayDownACap),
        };

        foreach (float height in new[] { -3f, 0f, 3f })
        {
            for (int step = 0; step < 6; step++)
            {
                float angle = step * MathF.PI / 3f;

                onTheBoundary.Add(new Vector3(2f * MathF.Cos(angle), height, 2f * MathF.Sin(angle)));
            }
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_capsule, onTheBoundary);
    }

    [Test]
    public void HoldsItsSurfaceWithinAMillimeter()
    {
        Vector3 alongX = new(1f, 0f, 0f);
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_capsule, alongX * 2f, alongX);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_capsule, up * 5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_capsule, down * 5f, down);
        });
    }

    [Test]
    public void HoldsAPositionExactlyTheTolerancePastItsSide()
    {
        // At the world's origin and unturned, a position comes into the capsule's own coordinates unchanged, so this
        // one, beside the middle of the segment at the radius and the tolerance added in float, has exactly the squared
        // distance the capsule compares with, which only an inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(2f + 0.001f, 0f, 0f);

        _capsule.Origin = Vector3.Zero;

        Assert.That(_capsule.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsTipsWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // A distance from the segment mixes all three axes, so the tolerance is taken along the axis with the smallest
        // scale, up in each case here, and is 1 mm in the world out from either tip.
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);

        _capsule.Rotation = RegionSpecHelpers.AwkwardTurn;
        _capsule.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_capsule, up * 5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_capsule, down * 5f, down);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_capsule);
    }

    [Test]
    public void SpreadsSamplesIntoItsCaps()
    {
        // HalfHeight 5 and Radius 2 leave a segment reaching 3 either side of the middle at y = 5. A sample above y = 8
        // is then in the upper cap and one below y = 2 in the lower. A sampler that guessed only in the eighth of its
        // box past Origin would still land every sample inside the capsule and none in the lower cap, so the test asks
        // for a sample in each cap. Each cap is 2/13 of the capsule, so the chance that 200 uniform samples all miss a
        // given cap is (11/13) to the 200th, about 3 in 10 to the 15th.
        const int sampleCount = 200;

        bool sawInTheUpperCap = false;
        bool sawInTheLowerCap = false;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            _capsule.TryGetRandomPosition(out Vector3 position);

            sawInTheUpperCap |= position.Y > 8f;
            sawInTheLowerCap |= position.Y < 2f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawInTheUpperCap, Is.True);
            Assert.That(sawInTheLowerCap, Is.True);
        });
    }

    [Test]
    public void FollowsItsOriginTarget()
    {
        // The capsule is built at the default Origin, so the target's position alone places it. Each point lies 1 to
        // the side of the position it is named for and level with it. The answers then turn on where the capsule
        // stands and not on how far its segment reaches.
        Vector3 besideTheFirstPosition = new(101f, 0f, 100f);
        Vector3 besideTheSecondPosition = new(201f, 0f, 200f);
        Vector3 besideTheWorldsOrigin = new(1f, 0f, 0f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };

        CapsuleRegion capsule = new()
        {
            HalfHeight = 3f,
            Radius = 2f,
            OriginTarget = target,
        };

        Assert.Multiple(() =>
        {
            Assert.That(capsule.IsInRegion(besideTheFirstPosition), Is.True);
            Assert.That(capsule.IsInRegion(besideTheWorldsOrigin), Is.False, "where it stands without the target");
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(capsule.IsInRegion(besideTheSecondPosition), Is.True);
    }

    [Test]
    public void ReachesAlongItsSegmentOnceTilted()
    {
        // Pitched a quarter turn, the capsule's up lies along the world's Z. A point 4 from the middle along the tilt
        // is 1 past the segment's end and inside the cap. A point 4 above the middle is 4 from the segment.
        Vector3 inTheCapAlongTheTilt = new(10f, 5f, 14f);
        Vector3 aboveTheMiddle = new(10f, 9f, 10f);

        _capsule.Rotation = Rotation.FromEulerAngles(90f, 0f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_capsule.IsInRegion(inTheCapAlongTheTilt), Is.True);
            Assert.That(_capsule.IsInRegion(aboveTheMiddle), Is.False, "the upright capsule holds it");
        });
    }

    [Test]
    public void MeasuresItsHalfHeightWithItsCaps()
    {
        // HalfHeight 5 and Radius 1 leave a segment reaching 4 either way, so the tip of the upper cap is 5 above the
        // middle. A segment reaching HalfHeight would put the tip at 6.
        Vector3 justUnderTheTip = new(0f, 4.9f, 0f);
        Vector3 justOverTheTip = new(0f, 5.1f, 0f);

        CapsuleRegion capsule = new()
        {
            HalfHeight = 5f,
            Radius = 1f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(capsule.IsInRegion(justUnderTheTip), Is.True);
            Assert.That(capsule.IsInRegion(justOverTheTip), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTilted()
    {
        _capsule.Rotation = Rotation.FromEulerAngles(45f, 0f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_capsule);
    }

    [Test]
    public void ActsAsABallWhenItsHalfHeightIsUnderItsRadius()
    {
        // HalfHeight 1 under Radius 2 leaves no segment, so the capsule is the ball of radius 2. About a sixth of that
        // ball lies above y = 1, where a sample box reaching only HalfHeight never puts a sample.
        const int sampleCount = 200;

        Vector3 justInsideTheBall = new(0f, 1.9f, 0f);
        Vector3 justOutsideTheBall = new(0f, 2.1f, 0f);

        CapsuleRegion capsule = new()
        {
            HalfHeight = 1f,
            Radius = 2f,
            RandomSource = _randomSource,
        };

        bool sawAboveTheHalfHeight = false;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            capsule.TryGetRandomPosition(out Vector3 position);

            sawAboveTheHalfHeight |= position.Y > 1f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(capsule.IsInRegion(justInsideTheBall), Is.True);
            Assert.That(capsule.IsInRegion(justOutsideTheBall), Is.False);
            Assert.That(sawAboveTheHalfHeight, Is.True, "a sample lies above y = 1");
        });
    }
}
