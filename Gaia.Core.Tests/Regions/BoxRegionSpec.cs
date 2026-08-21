using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="BoxRegion" />.</summary>
[TestFixture]
public class BoxRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _box = new BoxRegion
        {
            Min = new Vector3(0f, 0f, 0f),
            Max = new Vector3(10f, 3f, 20f),
            RandomSource = _randomSource,
        };
    }

    private SystemRandomSource _randomSource;

    private BoxRegion _box;

    [Test]
    public void ContainsWhatIsBetweenItsCorners()
    {
        Vector3 middle = new(5f, 1f, 10f);
        Vector3 onCeiling = new(5f, 3f, 10f);
        Vector3 aboveCeiling = new(5f, 4f, 10f);
        Vector3 belowFloor = new(5f, -1f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_box.IsInRegion(middle), Is.True);
            Assert.That(_box.IsInRegion(onCeiling), Is.True, "the ceiling counts as inside");
            Assert.That(_box.IsInRegion(aboveCeiling), Is.False);
            Assert.That(_box.IsInRegion(belowFloor), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_box);
    }

    [Test]
    public void ReachesItsExtentsOutFromItsCenter()
    {
        Vector3 center = new(5f, 1f, 10f);
        Vector3 extents = new(5f, 2f, 10f);
        Vector3 expectedMin = new(0f, -1f, 0f);
        Vector3 expectedMax = new(10f, 3f, 20f);

        BoxRegion box = new(center, extents);

        Assert.Multiple(() =>
        {
            Assert.That(box.Min, Is.EqualTo(expectedMin));
            Assert.That(box.Max, Is.EqualTo(expectedMax));
            Assert.That(box.IsInRegion(center), Is.True);
        });
    }
}
