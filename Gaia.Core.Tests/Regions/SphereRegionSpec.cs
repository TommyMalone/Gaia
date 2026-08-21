using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="SphereRegion" />.</summary>
[TestFixture]
public class SphereRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _sphere = new SphereRegion
        {
            Center = new Vector3(10f, 0f, 10f),
            Radius = 5f,
            RandomSource = _randomSource,
        };
    }

    private SystemRandomSource _randomSource;

    private SphereRegion _sphere;

    [Test]
    public void ContainsWhatIsWithinItsRadius()
    {
        Vector3 center = new(10f, 0f, 10f);
        Vector3 onEdge = new(10f, 5f, 10f);
        Vector3 justOutside = new(10f, 6f, 10f);
        Vector3 boundingCubeCorner = new(13f, 3f, 13f);

        Assert.Multiple(() =>
        {
            Assert.That(_sphere.IsInRegion(center), Is.True);
            Assert.That(_sphere.IsInRegion(onEdge), Is.True, "the edge counts as inside");
            Assert.That(_sphere.IsInRegion(justOutside), Is.False);
            Assert.That(_sphere.IsInRegion(boundingCubeCorner), Is.False, "the corner of the bounding cube is outside");
        });
    }

    [Test]
    public void TakesHeightIntoAccount()
    {
        Vector3 farAboveCenter = new(10f, 100f, 10f);

        Assert.That(_sphere.IsInRegion(farAboveCenter), Is.False);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_sphere);
    }

    [Test]
    public void ContainsNothingWhenItsCenterTargetHasNoPosition()
    {
        Vector3 initialCenter = new(10f, 0f, 10f);

        SelfTarget target = new();
        _sphere.CenterTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_sphere.IsInRegion(initialCenter), Is.False);
            Assert.That(_sphere.TryGetRandomPosition(out _), Is.False);
        });
    }
}
