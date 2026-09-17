using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="CircleRegion" />.</summary>
[TestFixture]
public class CircleRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        _circle = new CircleRegion
        {
            Origin = InitialOrigin,
            Radius = 5f,
            RandomSource = _randomSource,
        };
    }

    private static readonly Vector3 InitialOrigin = new(10f, 0f, 10f);

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

    private CircleRegion _circle;

    [Test]
    public void ContainsWhatIsWithinItsRadius()
    {
        Vector3 origin = new(10f, 0f, 10f);
        Vector3 insideNearEdge = new(14f, 0f, 10f);
        Vector3 onEdge = new(15f, 0f, 10f);
        Vector3 justOutside = new(16f, 0f, 10f);
        Vector3 boundingSquareCorner = new(14f, 0f, 14f);

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(origin), Is.True);
            Assert.That(_circle.IsInRegion(insideNearEdge), Is.True);
            Assert.That(_circle.IsInRegion(onEdge), Is.True, "the edge counts as inside");
            Assert.That(_circle.IsInRegion(justOutside), Is.False);
            Assert.That(_circle.IsInRegion(boundingSquareCorner), Is.False, "the bounding square's corner is outside");
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveOrigin = new(10f, 100f, 10f);
        Vector3 farBelowOrigin = new(10f, -100f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(farAboveOrigin), Is.True);
            Assert.That(_circle.IsInRegion(farBelowOrigin), Is.True);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_circle);
    }

    [Test]
    public void ReportsASampleAtItsOriginsHeight()
    {
        _circle.Origin = new Vector3(10f, 7f, 10f);

        _circle.TryGetRandomPosition(out Vector3 position);

        Assert.That(position.Y, Is.EqualTo(7f));
    }

    [Test]
    public void StandsOnEdgeWithItsRotation()
    {
        // Pitched a quarter turn, the disc stands upright across the world's Z, so it ignores Z instead of height.
        Vector3 farAlongZ = new(10f, 4f, 20f);
        Vector3 farAboveOrigin = new(10f, 100f, 10f);

        _circle.Rotation = Rotation.FromEulerAngles(90f, 0f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(farAlongZ), Is.True);
            Assert.That(_circle.IsInRegion(farAboveOrigin), Is.False, "height is no longer ignored");
        });
    }

    [Test]
    public void SamplesInsideItselfOnEdge()
    {
        _circle.Rotation = Rotation.FromEulerAngles(90f, 0f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_circle);
    }

    [Test]
    public void FollowsItsOriginTarget()
    {
        Vector3 nearFirstTargetPosition = new(102f, 0f, 100f);
        Vector3 nearSecondTargetPosition = new(202f, 0f, 200f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };
        _circle.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(InitialOrigin), Is.False, "the initial origin no longer applies");
            Assert.That(_circle.IsInRegion(nearFirstTargetPosition), Is.True);
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(_circle.IsInRegion(nearSecondTargetPosition), Is.True);
    }

    [Test]
    public void ContainsNothingWhenItsOriginTargetHasNoPosition()
    {
        SelfTarget target = new();
        _circle.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(InitialOrigin), Is.False);
            Assert.That(_circle.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void LoadsItsGeometryAndOriginTarget()
    {
        const string snippet = """
                               <Region Type="Circle" Radius="5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CircleRegion loaded = new();

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
    public void RefusesANodeMissingItsRadius()
    {
        const string snippet = """
                               <Region Type="Circle" Origin="(0, 0, 0)"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CircleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has no Radius attribute",
            }));
        });
    }

    [Test]
    public void RefusesASecondOriginTarget()
    {
        const string snippet = """
                               <Region Type="Circle" Radius="5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                                   <OriginTarget Type="Position" Position="(200, 0, 200)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CircleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Region element takes only one OriginTarget element",
            }), "silently keeping one of two targets would ship half of what was written");
        });
    }
}
