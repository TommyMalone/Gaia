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
    public void ContainsItsSurfaceFarFromTheWorldsOriginOnceTurned()
    {
        // Six points around the side at its bottom, its middle and its top, and three points on each of its ends.
        List<Vector3> onTheBoundary = new()
        {
            new Vector3(0f, 1.5f, 0f), new Vector3(2.2f, 1.5f, -1.3f), new Vector3(-3.1f, 1.5f, 2.4f),
            new Vector3(0f, -1.5f, 0f), new Vector3(-0.6f, -1.5f, 4.1f), new Vector3(3.7f, -1.5f, -0.9f),
        };

        foreach (float height in new[] { -1.5f, 0f, 1.5f })
        {
            for (int step = 0; step < 6; step++)
            {
                float angle = step * MathF.PI / 3f;

                onTheBoundary.Add(new Vector3(5f * MathF.Cos(angle), height, 5f * MathF.Sin(angle)));
            }
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_cylinder, onTheBoundary);
    }

    [Test]
    public void HoldsItsSurfaceWithinAMillimeter()
    {
        Vector3 alongX = new(1f, 0f, 0f);
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_cylinder, alongX * 5f, alongX);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_cylinder, up * 1.5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_cylinder, down * 1.5f, down);
        });
    }

    [TestCase(5f + 0.001f, 0f)]
    [TestCase(0f, 1.5f + 0.001f)]
    public void HoldsAPositionExactlyTheTolerancePastItsSideOrAnEnd(float x, float y)
    {
        // At the world's origin and unturned, a position comes into the cylinder's own coordinates unchanged, so this
        // one lies exactly where the cylinder's comparison of its side or its top falls, the radius or the half height
        // and the tolerance added in float, which only an inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(x, y, 0f);

        _cylinder.Origin = Vector3.Zero;

        Assert.That(_cylinder.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsSurfaceWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // The top and the bottom take the tolerance along local Y. A distance from the axis mixes local X and Z, so it
        // takes the tolerance along the one with the smaller scale, Z in each case here, and is 1 mm in the world along
        // it.
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);
        Vector3 alongZ = new(0f, 0f, 1f);

        _cylinder.Rotation = RegionSpecHelpers.AwkwardTurn;
        _cylinder.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_cylinder, up * 1.5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_cylinder, down * 1.5f, down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_cylinder, alongZ * 5f, alongZ);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_cylinder);
    }

    [Test]
    public void SpreadsSamplesAlongItsHeight()
    {
        // A height pick from zero to HalfHeight would still land every sample inside the cylinder, none of them below
        // its middle at y = 1.5, so the test asks for a sample on each side of the middle. The chance that fifty
        // uniform samples all lie on a given side of the middle is one in 2 to the 50th.
        bool sawLowerHalf = false;
        bool sawUpperHalf = false;

        for (int sampleIndex = 0; sampleIndex < RegionSpecHelpers.SampleCount; sampleIndex++)
        {
            _cylinder.TryGetRandomPosition(out Vector3 position);

            sawLowerHalf |= position.Y < 1.5f;
            sawUpperHalf |= position.Y >= 1.5f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawLowerHalf, Is.True);
            Assert.That(sawUpperHalf, Is.True);
        });
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
            }));
        });
    }

    [Test]
    public void ReadsANegativeHalfHeightAsItsSize()
    {
        Vector3 belowTheMiddle = new(10f, 1f, 10f);

        _cylinder.HalfHeight = -1.5f;

        Assert.Multiple(() =>
        {
            Assert.That(_cylinder.IsInRegion(belowTheMiddle), Is.True,
                "a HalfHeight of -1.5 stored as given holds nothing");
            Assert.That(_cylinder.HalfHeight, Is.EqualTo(1.5f));
        });
    }
}
