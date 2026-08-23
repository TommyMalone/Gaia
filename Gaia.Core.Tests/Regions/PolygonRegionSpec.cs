using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="PolygonRegion" />.</summary>
/// <remarks>
///     The footprint is an L, a 10 by 10 square with its upper right quarter missing, because a convex footprint
///     would not tell a crossing test from a bounding box.
/// </remarks>
[TestFixture]
public class PolygonRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _polygon = new PolygonRegion { RandomSource = _randomSource };
        RegionSpecHelpers.AddLShapedFootprint(_polygon);
    }

    private SystemRandomSource _randomSource;

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
    public void ContainsNothingWithFewerThanThreeCorners()
    {
        Vector3 lineStart = new(0f, 0f, 0f);
        Vector3 lineEnd = new(10f, 0f, 0f);
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
}
