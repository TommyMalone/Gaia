using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="SectorRegion" />.</summary>
/// <remarks>
///     Every point that decides an angle in a test of the opening lies to one side of the sector's forward or past
///     one edge, so a sector opened along its right, a half-angle read as the whole width or radians read as degrees fails
///     at least one test.
/// </remarks>
[TestFixture]
public class SectorRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _logSink = new RecordingLogSink();

        _sector = new SectorRegion
        {
            Angle = 60f,
            Range = 20f,
            HalfHeight = 1.5f,
            RandomSource = new Xoshiro128RandomSource(12345),
        };
    }

    /// <summary>The direction of the fixture's right side from its Origin, in its plane, 30 degrees right of forward.</summary>
    private static readonly Vector2 AlongTheRightSide = new(MathF.Sin(MathF.PI / 6f), MathF.Cos(MathF.PI / 6f));

    private RecordingLogSink _logSink;

    private SectorRegion _sector;

    [Test]
    public void OpensAlongItsForwardAcrossItsWholeAngle()
    {
        // (-5, 0, 10) is 26.6 degrees left of forward and (8, 0, 10) 38.7 degrees right. Opened along its right, the
        // sector puts the first 116.6 degrees off its axis. With the cosine of the whole Angle it opens 60 degrees to
        // either side, and with 30 taken as radians 81.1, and both hold the second.
        Vector3 leftOfForward = new(-5f, 0f, 10f);
        Vector3 pastTheRightEdge = new(8f, 0f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_sector.IsInRegion(leftOfForward), Is.True);
            Assert.That(_sector.IsInRegion(pastTheRightEdge), Is.False);
        });
    }

    [Test]
    public void OpensBehindItsOriginWhenWiderThanAHalfTurn()
    {
        // At 270 the sector opens 135 degrees to either side. (-5, 0, -1) is 101.3 degrees left of forward, behind
        // Origin, which the form for an opening of at most a half turn never holds. (1, 0, -5) is 168.7 degrees right.
        SectorRegion wide = new() { Angle = 270f, Range = 10f, HalfHeight = 1f };
        Vector3 behindAndLeft = new(-5f, 0f, -1f);
        Vector3 pastTheRightEdgeBehind = new(1f, 0f, -5f);

        Assert.Multiple(() =>
        {
            Assert.That(wide.IsInRegion(behindAndLeft), Is.True);
            Assert.That(wide.IsInRegion(pastTheRightEdgeBehind), Is.False);
        });
    }

    [Test]
    public void HoldsTheWholeDiscAtThreeHundredAndSixtyDegrees()
    {
        SectorRegion whole = new() { Angle = 360f, Range = 10f, HalfHeight = 1f };
        Vector3 ahead = new(0f, 0f, 5f);
        Vector3 straightBehind = new(0f, 0f, -5f);
        Vector3 behindAndLeft = new(-3f, 0f, -4f);
        Vector3 pastItsRange = new(0f, 0f, -10.5f);

        Assert.Multiple(() =>
        {
            Assert.That(whole.IsInRegion(ahead), Is.True);
            Assert.That(whole.IsInRegion(straightBehind), Is.True);
            Assert.That(whole.IsInRegion(behindAndLeft), Is.True);
            Assert.That(whole.IsInRegion(pastItsRange), Is.False);
        });
    }

    [Test]
    public void MeasuresItsRangeFromItsOriginInItsPlane()
    {
        // (0, 4, 19.8) is 19.8 from Origin in the plane and 20.2 in three dimensions. (10, 0, 18) is 29.1 degrees right
        // and 18 ahead, inside the width and inside the design's wedge 20 deep, but 20.6 from Origin.
        _sector.HalfHeight = 5f;

        Vector3 aboveThePlaneWithinRange = new(0f, 4f, 19.8f);
        Vector3 onTheFarEdge = new(0f, 0f, 20f);
        Vector3 pastTheArc = new(10f, 0f, 18f);

        Assert.Multiple(() =>
        {
            Assert.That(_sector.IsInRegion(aboveThePlaneWithinRange), Is.True);
            Assert.That(_sector.IsInRegion(onTheFarEdge), Is.True, "the far edge counts as inside");
            Assert.That(_sector.IsInRegion(pastTheArc), Is.False);
        });
    }

    [Test]
    public void StopsAHalfHeightEitherSideOfItsPlane()
    {
        _sector.Origin = new Vector3(0f, 1.5f, 0f);

        Vector3 belowThePlane = new(0f, 0.2f, 10f);
        Vector3 aboveTheTop = new(0f, 3.2f, 10f);
        Vector3 belowTheBottom = new(0f, -0.2f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_sector.IsInRegion(belowThePlane), Is.True);
            Assert.That(_sector.IsInRegion(aboveTheTop), Is.False);
            Assert.That(_sector.IsInRegion(belowTheBottom), Is.False);
        });
    }

    [Test]
    public void ReadsANegativeHalfHeightAsItsSize()
    {
        Vector3 belowThePlane = new(0f, 0.5f, 10f);

        _sector.Origin = new Vector3(0f, 1.5f, 0f);
        _sector.HalfHeight = -1.5f;

        Assert.Multiple(() =>
        {
            Assert.That(_sector.IsInRegion(belowThePlane), Is.True,
                "a HalfHeight of -1.5 stored as given holds nothing");
            Assert.That(_sector.HalfHeight, Is.EqualTo(1.5f));
        });
    }

    [Test]
    public void LoadsAHalfHeightOfZeroAsTheCylinderDoes()
    {
        const string snippet = """<Region Type="Sector" Origin="(0, 1.5, 0)" Angle="60" Range="20" HalfHeight="0"/>""";
        Vector3 inThePlane = new(0f, 1.5f, 10f);
        Vector3 aboveThePlane = new(0f, 1.6f, 10f);
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loaded?.IsInRegion(inThePlane), Is.True);
            Assert.That(loaded?.IsInRegion(aboveThePlane), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ContainsItsBoundaryFarFromTheWorldsOriginOnceTurned()
    {
        // Three points on its top and three on its bottom, and at each of the bottom, the plane and the top, three on
        // the far arc, three up each straight side and the apex, where the two straight sides meet.
        List<Vector3> onTheBoundary = new()
        {
            new Vector3(0f, 1.5f, 10f), new Vector3(-3f, 1.5f, 12f), new Vector3(2.5f, 1.5f, 6f),
            new Vector3(0f, -1.5f, 10f), new Vector3(-3f, -1.5f, 12f), new Vector3(2.5f, -1.5f, 6f),
        };

        foreach (float height in new[] { -1.5f, 0f, 1.5f })
        {
            foreach (float degreesOffForward in new[] { -25f, 0f, 25f })
            {
                float radians = degreesOffForward * MathF.PI / 180f;

                onTheBoundary.Add(new Vector3(20f * MathF.Sin(radians), height, 20f * MathF.Cos(radians)));
            }

            foreach (float alongTheSide in new[] { 5f, 12f, 19f })
            {
                Vector2 onTheRightSide = AlongTheRightSide * alongTheSide;

                onTheBoundary.Add(new Vector3(onTheRightSide.X, height, onTheRightSide.Y));
                onTheBoundary.Add(new Vector3(-onTheRightSide.X, height, onTheRightSide.Y));
            }

            onTheBoundary.Add(new Vector3(0f, height, 0f));
        }

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_sector, onTheBoundary);
    }

    [Test]
    public void HoldsItsBoundaryWithinAMillimeter()
    {
        // Each straight side's way out is square to it in the plane, and the apex's is straight back from Origin, where
        // the nearest point of either side is the apex itself.
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);
        Vector3 forward = new(0f, 0f, 1f);
        Vector3 back = new(0f, 0f, -1f);
        Vector3 onTheRightSide = new(AlongTheRightSide.X * 10f, 0f, AlongTheRightSide.Y * 10f);
        Vector3 onTheLeftSide = new(-onTheRightSide.X, 0f, onTheRightSide.Z);
        Vector3 outOfTheRightSide = new(AlongTheRightSide.Y, 0f, -AlongTheRightSide.X);
        Vector3 outOfTheLeftSide = new(-outOfTheRightSide.X, 0f, outOfTheRightSide.Z);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, new Vector3(0f, 1.5f, 10f), up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, new Vector3(0f, -1.5f, 10f), down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, forward * 20f, forward);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, onTheRightSide, outOfTheRightSide);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, onTheLeftSide, outOfTheLeftSide);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_sector, Vector3.Zero, back);
        });
    }

    [TestCase(0f, 0f, 20f + 0.001f)]
    [TestCase(0f, 1.5f + 0.001f, 10f)]
    [TestCase(0f, 0f, -0.001f)]
    public void HoldsAPositionExactlyTheTolerancePastItsArcOrAFace(float x, float y, float z)
    {
        // At the world's origin and unturned, a position comes into the sector's own coordinates unchanged. Each case
        // lies exactly the tolerance past the far arc, the top, or the apex where the two straight sides meet, so only
        // an inclusive comparison holds it.
        Vector3 exactlyTheTolerancePast = new(x, y, z);

        Assert.That(_sector.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsBoundaryWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // The top and the bottom take the tolerance along local Y. The far arc and the straight sides mix local X and
        // Z, so they take the tolerance along the axis with the smaller scale, Z where the two differ here, and the far
        // arc and the apex are measured along Z. The Range of 5 keeps the arc within 500 meters of Origin at the
        // largest scale, as the cylinder's radius does.
        Vector3 up = new(0f, 1f, 0f);
        Vector3 down = new(0f, -1f, 0f);
        Vector3 forward = new(0f, 0f, 1f);
        Vector3 back = new(0f, 0f, -1f);

        _sector.Range = 5f;
        _sector.Rotation = RegionSpecHelpers.AwkwardTurn;
        _sector.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sector, new Vector3(0f, 1.5f, 3f),
                up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sector, new Vector3(0f, -1.5f, 3f),
                down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sector, forward * 5f, forward);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_sector, Vector3.Zero, back);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _sector.Origin = new Vector3(10f, 0f, 0f);
        _sector.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_sector);
    }

    [Test]
    public void SpreadsItsSamplesEvenlyThroughItsVolume()
    {
        // A quarter of the slice's area lies within 5 of Origin, half of it to the left of forward, and half of its
        // volume below its plane. A pick without the square root puts half its samples within 5, a turn from 0 to the
        // whole Angle puts none to the left, and a height from 0 to HalfHeight puts none below. Each window is about
        // five spreads of 2,000 samples either side of the fraction expected.
        const int sampleCount = 2000;

        SectorRegion quarter = new()
        {
            Angle = 90f,
            Range = 10f,
            HalfHeight = 2f,
            RandomSource = new Xoshiro128RandomSource(12345),
        };
        int withinFive = 0;
        int toTheLeft = 0;
        int belowThePlane = 0;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            quarter.TryGetRandomPosition(out Vector3 position);

            withinFive += position.X * position.X + position.Z * position.Z <= 25f ? 1 : 0;
            toTheLeft += position.X < 0f ? 1 : 0;
            belowThePlane += position.Y < 0f ? 1 : 0;
        }

        Assert.Multiple(() =>
        {
            Assert.That(withinFive / (float)sampleCount, Is.InRange(0.2f, 0.3f), "within 5 of Origin in the plane");
            Assert.That(toTheLeft / (float)sampleCount, Is.InRange(0.4f, 0.6f), "to the left of forward");
            Assert.That(belowThePlane / (float)sampleCount, Is.InRange(0.4f, 0.6f), "below the plane");
        });
    }

    [Test]
    public void LoadsAFieldOfViewThatFollowsItsTarget()
    {
        // The design's wedge as a sector. The actor stands at (100, 0, 0) turned a quarter right, so the sector opens
        // along the world's X from (100, 1.5, 0). (110, 0.2, 4) comes in 21.8 degrees left of forward and 1.3 below the
        // plane, and (105, 1, -8) 58 degrees right. With the actor's turn ignored, the first would come in 68.2 degrees
        // right, outside.
        const string snippet = """
                               <Region Type="Sector" Origin="(0, 1.5, 0)" Angle="60" Range="20" HalfHeight="1.5">
                                   <OriginTarget Type="Self"/>
                               </Region>
                               """;
        Vector3 leftOfTheActorsFacing = new(110f, 0.2f, 4f);
        Vector3 pastTheRightEdge = new(105f, 1f, -8f);
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.Self = new Actor
        {
            Position = new Vector3(100f, 0f, 0f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
        };

        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? created);
        SectorRegion? loaded = created as SectorRegion;

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loaded?.Angle, Is.EqualTo(60f));
            Assert.That(loaded?.Range, Is.EqualTo(20f));
            Assert.That(loaded?.HalfHeight, Is.EqualTo(1.5f));
            Assert.That(loaded?.OriginTarget, Is.InstanceOf<SelfTarget>());
            Assert.That(loaded?.IsInRegion(leftOfTheActorsFacing), Is.True);
            Assert.That(loaded?.IsInRegion(pastTheRightEdge), Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAnAngleOutsideItsLimit()
    {
        Assert.Multiple(() =>
        {
            foreach (string angle in new[] { "0", "-60", "361" })
            {
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(
                    $"<Region Type=\"Sector\" Angle=\"{angle}\" Range=\"20\" HalfHeight=\"1.5\"/>", logSink);

                Assert.That(creationData.Factories.Regions.TryCreate(creationData, out _), Is.False, angle);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                {
                    $"Error guard.xml Line 1, the Region element's Angle is not above 0 and at most 360, it reads \"{angle}\"",
                    "Error guard.xml Line 1, the Region element's type Sector failed to initialize",
                }), angle);
            }

            RecordingLogSink wholeDiscSink = new();
            AiCreationData wholeDisc = LoadingSpecHelpers.ParseForLoad(
                "<Region Type=\"Sector\" Angle=\"360\" Range=\"20\" HalfHeight=\"1.5\"/>", wholeDiscSink);

            Assert.That(wholeDisc.Factories.Regions.TryCreate(wholeDisc, out _), Is.True, "an Angle of 360 loads");
            Assert.That(wholeDiscSink.Entries, Is.Empty, "an Angle of 360 loads");
        });
    }

    [Test]
    public void RefusesARangeAtOrBelowZero()
    {
        Assert.Multiple(() =>
        {
            foreach (string range in new[] { "0", "-20" })
            {
                RecordingLogSink logSink = new();
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(
                    $"<Region Type=\"Sector\" Angle=\"60\" Range=\"{range}\" HalfHeight=\"1.5\"/>", logSink);

                Assert.That(creationData.Factories.Regions.TryCreate(creationData, out _), Is.False, range);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                {
                    $"Error guard.xml Line 1, the Region element's Range is not above 0, it reads \"{range}\"",
                    "Error guard.xml Line 1, the Region element's type Sector failed to initialize",
                }), range);
            }
        });
    }
}
