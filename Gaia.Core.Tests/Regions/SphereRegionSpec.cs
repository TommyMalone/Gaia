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
    public void ContainsItsSurfaceFarFromTheWorldsOriginOnceTurned()
    {
        // Where each of the ball's own axes meets its surface, and where each of the eight diagonals between them does.
        float alongADiagonal = 5f / MathF.Sqrt(3f);
        List<Vector3> onTheBoundary = new()
        {
            new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f), new Vector3(0f, 5f, 0f), new Vector3(0f, -5f, 0f),
            new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, -5f),
        };

        foreach (float x in new[] { -alongADiagonal, alongADiagonal })
        {
            foreach (float y in new[] { -alongADiagonal, alongADiagonal })
            {
                foreach (float z in new[] { -alongADiagonal, alongADiagonal })
                {
                    onTheBoundary.Add(new Vector3(x, y, z));
                }
            }
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_sphere, onTheBoundary);
    }

    [Test]
    public void HoldsItsSurfaceWithinAMillimeter()
    {
        Vector3 alongX = new(1f, 0f, 0f);
        Vector3 up = new(0f, 1f, 0f);
        Vector3 againstZ = new(0f, 0f, -1f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sphere, alongX * 5f, alongX);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sphere, up * 5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sphere, againstZ * 5f, againstZ);
        });
    }

    [Test]
    public void ReachesAsFarWithANegativeRadius()
    {
        // A negative radius reaches as far as its size, so the tolerance is added to that size. Added to the negative
        // value, it would pull the surface in.
        Vector3 alongX = new(1f, 0f, 0f);

        _sphere.Radius = -5f;

        RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sphere, alongX * 5f, alongX);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsSurfaceWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // A distance from the middle mixes all three axes, so the tolerance is taken along the axis with the smallest
        // scale, up in each case here, and is 1 mm in the world along it. Along an axis scaled further it is more.
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);

        _sphere.Rotation = RegionSpecHelpers.AwkwardTurn;
        _sphere.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sphere, up * 5f, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sphere, down * 5f, down);
        });
    }

    [TestCase(0.5f, 2f, 8f, 1f, 0f, 0f)]
    [TestCase(8f, 2f, 0.5f, 0f, 0f, 1f)]
    public void HoldsItsSurfaceWithinAMillimeterAlongWhicheverAxisIsScaledLeast(
        float scaleX,
        float scaleY,
        float scaleZ,
        float axisX,
        float axisY,
        float axisZ)
    {
        // The smallest scale lies on X in the first case and on Z in the second, where
        // HoldsItsSurfaceWithinAMillimeterInTheWorldAtAnyScale has it on up, so the tolerance taken along the axis with
        // the smallest scale is pinned on all three axes, 1 mm in the world along each when it is the one scaled least.
        Vector3 axis = new(axisX, axisY, axisZ);

        _sphere.Rotation = RegionSpecHelpers.AwkwardTurn;
        _sphere.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sphere, axis * 5f, axis);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sphere, axis * -5f, axis * -1f);
        });
    }

    [Test]
    public void HoldsAPositionExactlyTheTolerancePastItsSurface()
    {
        // At the world's origin and unturned, a position comes into the ball's own coordinates unchanged, so this one,
        // at the radius and the tolerance added in float, has exactly the squared length the ball compares with, which
        // only an inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(5f + 0.001f, 0f, 0f);

        _sphere.Origin = Vector3.Zero;

        Assert.That(_sphere.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_sphere);
    }

    [Test]
    public void SamplesBelowItsOriginAlongEveryAxis()
    {
        // A sampler that guessed from zero to Radius along each axis would still land every sample inside the ball, in
        // the eighth of it past Origin, so the test asks for a sample below Origin on each axis. The chance that fifty
        // uniform samples all lie past Origin on one axis is one in 2 to the 50th.
        bool sawBelowOriginAlongX = false;
        bool sawBelowOriginAlongY = false;
        bool sawBelowOriginAlongZ = false;

        for (int sampleIndex = 0; sampleIndex < RegionSpecHelpers.SampleCount; sampleIndex++)
        {
            _sphere.TryGetRandomPosition(out Vector3 position);

            sawBelowOriginAlongX |= position.X < 10f;
            sawBelowOriginAlongY |= position.Y < 0f;
            sawBelowOriginAlongZ |= position.Z < 10f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawBelowOriginAlongX, Is.True);
            Assert.That(sawBelowOriginAlongY, Is.True);
            Assert.That(sawBelowOriginAlongZ, Is.True);
        });
    }

    [Test]
    public void SpreadsItsSamplesEvenlyThroughItsVolume()
    {
        // An eighth of the ball's volume lies within half its Radius of Origin, and 0.488 of it lies more than 4 from
        // Origin, in the shell the outer fifth of the Radius makes. Guesses from a box half as wide as the one around
        // the ball land inside every time. They put about half the samples within half the Radius and almost none in
        // the shell. Each window is about five spreads of 2,000 samples either side of the fraction expected, so a
        // uniform pick falls outside one with a chance under one in a million. The seeded source draws the same 2,000
        // every run. The capsule and the convex hull guess through the same TryRejectionSample.
        const int sampleCount = 2000;
        Vector3 origin = new(10f, 0f, 10f);
        int withinHalfTheRadius = 0;
        int inTheOuterShell = 0;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            _sphere.TryGetRandomPosition(out Vector3 position);

            float squaredDistance = (position - origin).SquaredMagnitude;

            withinHalfTheRadius += squaredDistance <= 6.25f ? 1 : 0;
            inTheOuterShell += squaredDistance > 16f ? 1 : 0;
        }

        Assert.Multiple(() =>
        {
            Assert.That(withinHalfTheRadius / (float)sampleCount, Is.InRange(0.085f, 0.165f),
                "within half the Radius of Origin");
            Assert.That(inTheOuterShell / (float)sampleCount, Is.InRange(0.43f, 0.55f), "more than 4 from Origin");
        });
    }

    [Test]
    public void ReportsNoPositionWhenEveryGuessMisses()
    {
        // A source that always gives 0 guesses the lowest corner of the ball's box, about 8.7 from Origin and outside
        // the ball, so every attempt misses and the search has to give up.
        _sphere.RandomSource = new FixedRandomSource(0f);

        Assert.That(_sphere.TryGetRandomPosition(out _), Is.False);
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
    public void FollowsItsOriginTargetAsItMoves()
    {
        // The target's transform is read at every query rather than once when the target is set, so the ball is around
        // the target's new position once the target has moved.
        const string snippet = """
                               <Region Type="Sphere" Radius="5">
                                   <OriginTarget Type="Position" Position="(100, 0, 100)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        Vector3 nearTheFirstPosition = new(102f, 0f, 102f);
        Vector3 nearTheSecondPosition = new(202f, 0f, 202f);

        SphereRegion loaded = new();
        Assert.That(loaded.Init(creationData), Is.True);

        PositionTarget target = (PositionTarget)loaded.OriginTarget!;
        target.Position = new Vector3(200f, 0f, 200f);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.IsInRegion(nearTheSecondPosition), Is.True);
            Assert.That(loaded.IsInRegion(nearTheFirstPosition), Is.False, "where the target stood when it was loaded");
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
    public void LoadsItsScaleAndAbsoluteScale()
    {
        // The ellipsoid above, loaded. (0, 1.9, 0) comes in at 0.95 up and (1.9, 0, 0) at 1.9 along its right.
        const string snippet = """<Region Type="Sphere" Radius="1" Scale="(1, 2, 1)" AbsoluteScale="true"/>""";
        Vector3 nearTheStretchedTop = new(0f, 1.9f, 0f);
        Vector3 asFarAlongItsRight = new(1.9f, 0f, 0f);
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loaded?.Scale, Is.EqualTo(new Vector3(1f, 2f, 1f)));
            Assert.That(loaded?.AbsoluteScale, Is.True);
            Assert.That(loaded?.AbsoluteRotation, Is.False);
            Assert.That(loaded?.IsInRegion(nearTheStretchedTop), Is.True);
            Assert.That(loaded?.IsInRegion(asFarAlongItsRight), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAMalformedScaleFlagOrTarget()
    {
        // Each read reports what it could not read, so the load stops there rather than placing the ball with a value
        // that was never written. A Scale written as one number, as a uniform scale might be, is the likeliest.
        (string snippet, string[] reports)[] catalog =
        {
            ("<Region Type=\"Sphere\" Radius=\"1\" Scale=\"2\"/>", new[]
            {
                "Error guard.xml Line 1, the Region element's Scale is not a tuple like (0, 1.5, 300), it reads \"2\"",
            }),
            ("<Region Type=\"Sphere\" Radius=\"1\" AbsoluteRotation=\"yes\"/>", new[]
            {
                "Error guard.xml Line 1, the Region element's AbsoluteRotation is not true or false, it reads \"yes\"",
            }),
            ("<Region Type=\"Sphere\" Radius=\"1\" AbsoluteScale=\"yes\"/>", new[]
            {
                "Error guard.xml Line 1, the Region element's AbsoluteScale is not true or false, it reads \"yes\"",
            }),
            ("<Region Type=\"Sphere\" Radius=\"1\">\n    <OriginTarget Type=\"Position\"/>\n</Region>", new[]
            {
                "Error guard.xml Line 2, the OriginTarget element has no Position attribute",
                "Error guard.xml Line 2, the OriginTarget element's type Position failed to initialize",
            }),
        };

        Assert.Multiple(() =>
        {
            foreach ((string snippet, string[] reports) in catalog)
            {
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                SphereRegion loaded = new();

                Assert.That(loaded.Init(creationData), Is.False, snippet);
                Assert.That(logSink.Entries, Is.EqualTo(reports), snippet);
            }
        });
    }

    [Test]
    public void HoldsAMillimeterPastItsSurfaceAtALoadedScale()
    {
        // Scaled to 2, the ball's surface is 2 from its middle, and the tolerance of 1 millimeter in the world is
        // half a millimeter in its own units. A tolerance left at 1 millimeter in its own units would hold 2
        // millimeters past the surface in the world, and (2.0015, 0, 0) with it.
        const string snippet = """<Region Type="Sphere" Radius="1" Scale="(2, 2, 2)"/>""";
        Vector3 halfAMillimeterPast = new(2.0005f, 0f, 0f);
        Vector3 oneAndAHalfMillimetersPast = new(2.0015f, 0f, 0f);
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SphereRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.IsInRegion(halfAMillimeterPast), Is.True);
            Assert.That(loaded.IsInRegion(oneAndAHalfMillimetersPast), Is.False);
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
