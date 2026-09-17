using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="SphereRegion" />.</summary>
[TestFixture]
public class SphereRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        _sphere = new SphereRegion
        {
            Origin = new Vector3(10f, 0f, 10f),
            Radius = 5f,
            RandomSource = _randomSource,
        };
    }

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private SphereRegion _sphere;

    [Test]
    public void ContainsWhatIsWithinItsRadius()
    {
        Vector3 origin = new(10f, 0f, 10f);
        Vector3 onEdge = new(10f, 5f, 10f);
        Vector3 justOutside = new(10f, 6f, 10f);
        Vector3 boundingCubeCorner = new(13f, 3f, 13f);

        Assert.Multiple(() =>
        {
            Assert.That(_sphere.IsInRegion(origin), Is.True);
            Assert.That(_sphere.IsInRegion(onEdge), Is.True, "the edge counts as inside");
            Assert.That(_sphere.IsInRegion(justOutside), Is.False);
            Assert.That(_sphere.IsInRegion(boundingCubeCorner), Is.False, "the corner of the bounding cube is outside");
        });
    }

    [Test]
    public void TakesHeightIntoAccount()
    {
        Vector3 farAboveOrigin = new(10f, 100f, 10f);

        Assert.That(_sphere.IsInRegion(farAboveOrigin), Is.False);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_sphere);
    }

    [Test]
    public void ContainsNothingWhenItsOriginTargetHasNoPosition()
    {
        Vector3 initialOrigin = new(10f, 0f, 10f);

        SelfTarget target = new();
        _sphere.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_sphere.IsInRegion(initialOrigin), Is.False);
            Assert.That(_sphere.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void LoadsItsGeometryAndOriginTarget()
    {
        const string snippet = """
                               <Region Type="Sphere" Radius="5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SphereRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Radius, Is.EqualTo(5f));
            Assert.That(loaded.OriginTarget, Is.InstanceOf<PositionTarget>());
            Assert.That(loaded.IsInRegion(new Vector3(102f, 0f, 102f)), Is.True,
                "containment follows the loaded origin target, and no Origin need be written");
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 2f)), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesASecondOriginTarget()
    {
        const string snippet = """
                               <Region Type="Sphere" Radius="5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                                   <OriginTarget Type="Position" Position="(200, 0, 200)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SphereRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Region element takes only one OriginTarget element",
            }));
        });
    }
}
