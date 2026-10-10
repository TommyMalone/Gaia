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
    public void ScalesIntoAnEllipsoid()
    {
        Vector3 nearTheStretchedTop = new(0f, 1.9f, 0f);
        Vector3 asFarAlongItsRight = new(1.9f, 0f, 0f);

        SphereRegion ball = new()
        {
            Radius = 1f,
            Scale = new Vector3(1f, 2f, 1f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(ball.IsInRegion(nearTheStretchedTop), Is.True);
            Assert.That(ball.IsInRegion(asFarAlongItsRight), Is.False);
        });
    }

    [Test]
    public void ContainsTheSameBallWhateverItsRotation()
    {
        // A turn about all three axes leaves rounding in every coordinate of a position brought in, so each
        // point stands at least 0.1 clear of the edge.
        Vector3 insideAlongX = new(14.9f, 0f, 10f);
        Vector3 insideBelow = new(10f, -4.9f, 10f);
        Vector3 insideOffTheAxes = new(13f, 2f, 13f);
        Vector3 outsideAlongZ = new(10f, 0f, 15.1f);
        Vector3 outsideAbove = new(10f, 5.1f, 10f);
        Vector3 boundingCubeCorner = new(13f, 3f, 13f);

        _sphere.Rotation = Rotation.FromEulerAngles(30f, 45f, 60f);

        Assert.Multiple(() =>
        {
            Assert.That(_sphere.IsInRegion(insideAlongX), Is.True);
            Assert.That(_sphere.IsInRegion(insideBelow), Is.True);
            Assert.That(_sphere.IsInRegion(insideOffTheAxes), Is.True);
            Assert.That(_sphere.IsInRegion(outsideAlongZ), Is.False);
            Assert.That(_sphere.IsInRegion(outsideAbove), Is.False);
            Assert.That(_sphere.IsInRegion(boundingCubeCorner), Is.False);
        });
    }

    [Test]
    public void AsksItsTargetOncePerSample()
    {
        // A ball fills just over half the cube around it, so a sphere that asked its target for each guess would ask
        // nearly twice per sample. The count holds whatever the guesses are.
        const int sampleCount = 100;

        CountingTarget target = new(new Vector3(10f, 0f, 10f));
        _sphere.OriginTarget = target;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            _sphere.TryGetRandomPosition(out _);
        }

        Assert.That(target.AskCount, Is.EqualTo(sampleCount));
    }

    [Test]
    public void KeepsItsScaleWhenItsRotationIsSet()
    {
        // A yaw turns the ball about its own up, so the stretched top stays where it is while the scale is kept.
        Vector3 nearTheStretchedTop = new(0f, 1.9f, 0f);

        SphereRegion ball = new()
        {
            Radius = 1f,
            Scale = new Vector3(1f, 2f, 1f),
        };
        ball.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.That(ball.IsInRegion(nearTheStretchedTop), Is.True);
    }

    [Test]
    public void KeepsItsScaleWhenItsOriginIsSet()
    {
        // The move keeps the stretch, so the top is still 2 above the new middle.
        Vector3 nearTheStretchedTop = new(10f, 1.9f, 10f);

        SphereRegion ball = new()
        {
            Radius = 1f,
            Scale = new Vector3(1f, 2f, 1f),
        };
        ball.Origin = new Vector3(10f, 0f, 10f);

        Assert.That(ball.IsInRegion(nearTheStretchedTop), Is.True);
    }

    /// <summary>A target at a fixed position that counts how often it is asked for a position or a transform.</summary>
    private sealed class CountingTarget : TargetBase
    {
        private readonly Vector3 _position;

        /// <summary>Creates a target that answers the given position and has not yet been asked.</summary>
        public CountingTarget(Vector3 position)
        {
            _position = position;
        }

        /// <summary>How many times TryGetPosition and TryGetTransform have been called, between them.</summary>
        public int AskCount { get; private set; }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            AskCount++;
            position = _position;
            return true;
        }

        /// <inheritdoc />
        /// <remarks>Answers without calling TryGetPosition, so a request for the transform counts once.</remarks>
        public override bool TryGetTransform(out Transform transform)
        {
            AskCount++;
            transform = Transform.Identity.WithOrigin(_position);
            return true;
        }
    }
}
