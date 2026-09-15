using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

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
            Center = new Vector3(10f, 5f, 10f),
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
}
