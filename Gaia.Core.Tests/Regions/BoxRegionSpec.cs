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
        _randomSource = new Xoshiro128RandomSource(12345);

        _box = new BoxRegion
        {
            Min = new Vector3(0f, 0f, 0f),
            Max = new Vector3(10f, 3f, 20f),
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;

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

    [Test]
    public void TurnsItsCornersAboutItsOrigin()
    {
        // The corners run 10 along X from Origin and 1 either way across it. A quarter turn right swings that length
        // from the world's X onto its Z, where the box runs from 0 to -10.
        Vector3 alongTheTurnedLength = new(0f, 0f, -5f);
        Vector3 alongTheUnturnedLength = new(5f, 0f, 0f);

        BoxRegion box = new()
        {
            Min = new Vector3(0f, -1f, -1f),
            Max = new Vector3(10f, 1f, 1f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(box.IsInRegion(alongTheTurnedLength), Is.True);
            Assert.That(box.IsInRegion(alongTheUnturnedLength), Is.False,
                "the unturned box and a box turned about its middle both hold it");
        });
    }
}
