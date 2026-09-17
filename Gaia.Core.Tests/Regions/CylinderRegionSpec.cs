using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="CylinderRegion" />.</summary>
[TestFixture]
public class CylinderRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new Xoshiro128RandomSource(12345);
        _logSink = new RecordingLogSink();

        _cylinder = new CylinderRegion
        {
            Origin = InitialOrigin,
            Radius = 5f,
            HalfHeight = 1.5f,
            RandomSource = _randomSource,
        };
    }

    private static readonly Vector3 InitialOrigin = new(10f, 1.5f, 10f);

    private Xoshiro128RandomSource _randomSource;
    private RecordingLogSink _logSink;

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
    public void StopsAHalfHeightEitherSideOfItsOrigin()
    {
        Vector3 onTop = new(10f, 3f, 10f);
        Vector3 aboveTheTop = new(10f, 4f, 10f);
        Vector3 belowTheBottom = new(10f, -1f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(onTop), Is.True, "the top counts as inside");
            Assert.That(_cylinder.IsInRegion(aboveTheTop), Is.False);
            Assert.That(_cylinder.IsInRegion(belowTheBottom), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_cylinder);
    }

    [Test]
    public void LiesOnItsSideWithItsRotation()
    {
        // Pitched a quarter turn, the axis runs along the world's Z, so the height bound applies to Z and the disc
        // reaches up and down instead.
        Vector3 wellAboveTheOrigin = new(10f, 6f, 10f);
        Vector3 withinTheHalfHeightAlongZ = new(10f, 1.5f, 11.5f);
        Vector3 beyondTheHalfHeightAlongZ = new(10f, 1.5f, 12f);
        Vector3 insideBeforeTurning = new(14f, 1.5f, 12f);

        _cylinder.Rotation = Rotation.FromEulerAngles(90f, 0f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(wellAboveTheOrigin), Is.True);
            Assert.That(_cylinder.IsInRegion(withinTheHalfHeightAlongZ), Is.True);
            Assert.That(_cylinder.IsInRegion(beyondTheHalfHeightAlongZ), Is.False);
            Assert.That(_cylinder.IsInRegion(insideBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnItsSide()
    {
        _cylinder.Rotation = Rotation.FromEulerAngles(90f, 0f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_cylinder);
    }

    [Test]
    public void FollowsItsOriginTarget()
    {
        Vector3 nearFirstTargetPosition = new(102f, 0f, 100f);
        Vector3 nearSecondTargetPosition = new(202f, 0f, 200f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };
        _cylinder.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(InitialOrigin), Is.False, "the initial origin no longer applies");
            Assert.That(_cylinder.IsInRegion(nearFirstTargetPosition), Is.True);
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(_cylinder.IsInRegion(nearSecondTargetPosition), Is.True);
    }

    [Test]
    public void ContainsNothingWhenItsOriginTargetHasNoPosition()
    {
        SelfTarget target = new();
        _cylinder.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(InitialOrigin), Is.False);
            Assert.That(_cylinder.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void LoadsItsGeometryAndOriginTarget()
    {
        const string snippet = """
                               <Region Type="Cylinder" Radius="5" HalfHeight="1.5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CylinderRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Radius, Is.EqualTo(5f));
            Assert.That(loaded.HalfHeight, Is.EqualTo(1.5f));
            Assert.That(loaded.OriginTarget, Is.InstanceOf<PositionTarget>());
            Assert.That(loaded.IsInRegion(new Vector3(102f, 1f, 102f)), Is.True,
                "containment follows the loaded origin target, and no Origin need be written");
            Assert.That(loaded.IsInRegion(new Vector3(2f, 1f, 2f)), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesASecondOriginTarget()
    {
        const string snippet = """
                               <Region Type="Cylinder" Radius="5" HalfHeight="1.5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                                   <OriginTarget Type="Position" Position="(200, 0, 200)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        CylinderRegion loaded = new();

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
