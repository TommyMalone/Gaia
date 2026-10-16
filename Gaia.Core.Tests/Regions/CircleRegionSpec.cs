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
    public void ContainsItsEdgeFarFromTheWorldsOriginOnceTurned()
    {
        // Twelve points around the disc's edge, one every 30 degrees.
        List<Vector3> onTheBoundary = new();

        for (int step = 0; step < 12; step++)
        {
            float angle = step * MathF.PI / 6f;

            onTheBoundary.Add(new Vector3(5f * MathF.Cos(angle), 0f, 5f * MathF.Sin(angle)));
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_circle, onTheBoundary);
    }

    [Test]
    public void HoldsItsEdgeWithinAMillimeter()
    {
        Vector3 alongX = new(1f, 0f, 0f);
        Vector3 againstZ = new(0f, 0f, -1f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_circle, alongX * 5f, alongX);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_circle, againstZ * 5f, againstZ);
        });
    }

    [Test]
    public void HoldsAPositionExactlyTheTolerancePastItsEdge()
    {
        // At the world's origin and unturned, a position comes into the disc's plane unchanged, so this one, at the
        // radius and the tolerance added in float, has exactly the squared length the disc compares with, which only an
        // inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(5f + 0.001f, 0f, 0f);

        _circle.Origin = Vector3.Zero;

        Assert.That(_circle.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsEdgeWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // A distance from the axis mixes the plane's two axes, local X and Z, so the tolerance is taken along the one
        // with the smaller scale, Z in each case here, and is 1 mm in the world along it.
        Vector3 alongZ = new(0f, 0f, 1f);
        Vector3 againstZ = new(0f, 0f, -1f);

        _circle.Rotation = RegionSpecHelpers.AwkwardTurn;
        _circle.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_circle, alongZ * 5f, alongZ);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_circle, againstZ * 5f, againstZ);
        });
    }

    [Test]
    public void HoldsItsEdgeWithinAMillimeterAlongXWhenXIsScaledLeast()
    {
        // The plane's smaller scale lies on X here, where HoldsItsEdgeWithinAMillimeterInTheWorldAtAnyScale has it on
        // Z, so the tolerance taken along the plane's axis with the smaller scale is pinned on both, 1 mm in the world
        // along X here.
        Vector3 alongX = new(1f, 0f, 0f);
        Vector3 againstX = new(-1f, 0f, 0f);

        _circle.Rotation = RegionSpecHelpers.AwkwardTurn;
        _circle.Scale = new Vector3(0.5f, 2f, 8f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_circle, alongX * 5f, alongX);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_circle, againstX * 5f, againstX);
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
    public void CarriesItsOriginAsAnOffsetFromItsTarget()
    {
        // The fixture's Origin of (10, 0, 10) is an offset from the target, so the disc's middle is at (110, 0, 110).
        // The target's own position is 14 from that middle, outside the disc of radius 5.
        Vector3 nearTheOffsetMiddle = new(112f, 0f, 110f);
        Vector3 besideTheTarget = new(102f, 0f, 100f);
        Vector3 nearTheSecondOffsetMiddle = new(212f, 0f, 210f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };
        _circle.OriginTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(InitialOrigin), Is.False, "Origin alone no longer places the disc");
            Assert.That(_circle.IsInRegion(nearTheOffsetMiddle), Is.True);
            Assert.That(_circle.IsInRegion(besideTheTarget), Is.False, "the target's position does not replace Origin");
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(_circle.IsInRegion(nearTheSecondOffsetMiddle), Is.True);
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
    public void LoadsItsAbsoluteRotation()
    {
        // The design's circle under a drone that banks and pitches as it flies, moving with it and never tilting with
        // it.
        const string snippet = """
                               <Region Type="Circle" Radius="3" AbsoluteRotation="true">
                                   <OriginTarget Type="Self"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loaded?.AbsoluteRotation, Is.True);
            Assert.That(loaded?.AbsoluteScale, Is.False);
            Assert.That(loaded?.OriginTarget, Is.InstanceOf<SelfTarget>());
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

        // The factory refuses the second child before Init runs, so the test loads through the factory rather than
        // calling Init.
        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Region element takes only one OriginTarget element",
            }), "silently keeping one of two targets would ship half of what was written");
        });
    }
}
