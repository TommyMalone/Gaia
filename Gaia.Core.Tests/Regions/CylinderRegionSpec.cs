using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="CylinderRegion" />.</summary>
[TestFixture]
public class CylinderRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);

        _cylinder = new CylinderRegion
        {
            Center = new Vector3(10f, 0f, 10f),
            Radius = 5f,
            Floor = 0f,
            Ceiling = 3f,
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;

    private CylinderRegion _cylinder;

    [Test]
    public void ContainsWhatIsWithinItsRadius()
    {
        Vector3 middle = new(10f, 1f, 10f);
        Vector3 onEdge = new(15f, 1f, 10f);
        Vector3 justOutside = new(16f, 1f, 10f);
        Vector3 boundingSquareCorner = new(14f, 1f, 14f);

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(middle), Is.True);
            Assert.That(_cylinder.IsInRegion(onEdge), Is.True, "the edge counts as inside");
            Assert.That(_cylinder.IsInRegion(justOutside), Is.False);
            Assert.That(_cylinder.IsInRegion(boundingSquareCorner), Is.False,
                "the bounding square's corner is outside");
        });
    }

    [Test]
    public void StopsAtItsFloorAndCeiling()
    {
        Vector3 onCeiling = new(10f, 3f, 10f);
        Vector3 aboveCeiling = new(10f, 4f, 10f);
        Vector3 belowFloor = new(10f, -1f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(onCeiling), Is.True, "the ceiling counts as inside");
            Assert.That(_cylinder.IsInRegion(aboveCeiling), Is.False);
            Assert.That(_cylinder.IsInRegion(belowFloor), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_cylinder);
    }
}
