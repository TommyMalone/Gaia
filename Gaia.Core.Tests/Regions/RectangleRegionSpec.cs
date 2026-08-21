using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="RectangleRegion" />.</summary>
[TestFixture]
public class RectangleRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _rectangle = new RectangleRegion
        {
            Min = new Vector3(0f, 0f, 0f),
            Max = new Vector3(10f, 0f, 20f),
            RandomSource = _randomSource,
        };
    }

    private SystemRandomSource _randomSource;

    private RectangleRegion _rectangle;

    [Test]
    public void ContainsWhatIsBetweenItsCorners()
    {
        Vector3 middle = new(5f, 0f, 10f);
        Vector3 nearCorner = new(0f, 0f, 0f);
        Vector3 farCorner = new(10f, 0f, 20f);
        Vector3 pastTheNearSide = new(-1f, 0f, 10f);
        Vector3 pastTheFarSide = new(5f, 0f, 21f);

        Assert.Multiple(() =>
        {
            Assert.That(_rectangle.IsInRegion(middle), Is.True);
            Assert.That(_rectangle.IsInRegion(nearCorner), Is.True, "the corner counts as inside");
            Assert.That(_rectangle.IsInRegion(farCorner), Is.True, "the far corner counts as inside");
            Assert.That(_rectangle.IsInRegion(pastTheNearSide), Is.False);
            Assert.That(_rectangle.IsInRegion(pastTheFarSide), Is.False);
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveMiddle = new(5f, 100f, 10f);

        Assert.That(_rectangle.IsInRegion(farAboveMiddle), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_rectangle);
    }
}
