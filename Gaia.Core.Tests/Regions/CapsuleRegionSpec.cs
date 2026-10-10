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
            HalfHeight = 3f,
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
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_capsule);
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
}
