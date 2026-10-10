using Gaia.Core.Considerations;
using Gaia.Core.Curves;
using Gaia.Core.Entities;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;

namespace Gaia.Core.HostSpecs;

/// <summary>Proves that game code can build regions, targets and curves and query them through the public surface alone.</summary>
/// <remarks>
///     A region is queried by the host, not by the library. The library fills no <see cref="Contact" /> list, so a
///     host tests each contact against a region itself and writes the ones inside to the blackboard entity list that a
///     configuration's <see cref="EntityExistsConsideration" /> reads, which is the work this fixture does through the
///     public surface alone.
/// </remarks>
[TestFixture]
public class GeometryHostSpec
{
    [Test]
    public void BuildsARegionAndSortsPositionsWithIt()
    {
        CircleRegion killZone = new()
        {
            Origin = new Vector3(0f, 0f, 150f),
            Radius = 40f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(killZone.IsInRegion(new Vector3(10f, 0f, 140f)), Is.True);
            Assert.That(killZone.IsInRegion(new Vector3(0f, 0f, 0f)), Is.False);
        });
    }

    [Test]
    public void SamplesAPositionFromAHandBuiltRegion()
    {
        BoxRegion spawnArea = new()
        {
            Origin = new Vector3(0f, 1f, 0f),
            Extents = new Vector3(5f, 1f, 5f),
            RandomSource = new Xoshiro128RandomSource(7),
        };

        Assert.Multiple(() =>
        {
            Assert.That(spawnArea.TryGetRandomPosition(out Vector3 position), Is.True);
            Assert.That(spawnArea.IsInRegion(position), Is.True, "what the region hands out is inside it");
        });
    }

    [Test]
    public void TurnsAHandBuiltRegion()
    {
        // A corridor that runs along the world's X is a rectangle turned a quarter turn, which a host writes as a
        // rotation rather than by working out the turned corners.
        RectangleRegion corridor = new()
        {
            Origin = new Vector3(0f, 0f, 0f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Min = new Vector2(-2f, 0f),
            Max = new Vector2(2f, 50f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(corridor.IsInRegion(new Vector3(25f, 0f, 0f)), Is.True, "along the world's X once turned");
            Assert.That(corridor.IsInRegion(new Vector3(0f, 0f, 25f)), Is.False, "where it would lie unturned");
        });
    }

    [Test]
    public void BuildsABoxFromItsCenterAndExtents()
    {
        Vector3 center = new(0f, 1f, 0f);
        Vector3 extents = new(5f, 1f, 5f);

        BoxRegion spawnArea = new(center, extents);

        Assert.Multiple(() =>
        {
            Assert.That(spawnArea.Origin, Is.EqualTo(center));
            Assert.That(spawnArea.Extents, Is.EqualTo(extents));
            Assert.That(spawnArea.IsInRegion(center), Is.True);
        });
    }

    [Test]
    public void TurnsAHandBuiltBox()
    {
        // The box reaches 10 along its own right and 1 along its up and its forward. A quarter turn right swings its
        // length from the world's X onto the world's Z.
        Vector3 alongTheTurnedLength = new(0f, 1f, -8f);
        Vector3 alongTheUnturnedLength = new(8f, 1f, 0f);

        BoxRegion plank = new()
        {
            Origin = new Vector3(0f, 1f, 0f),
            Extents = new Vector3(10f, 1f, 1f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(plank.IsInRegion(alongTheTurnedLength), Is.True, "along the world's Z once turned");
            Assert.That(plank.IsInRegion(alongTheUnturnedLength), Is.False, "where it would lie unturned");
        });
    }

    [Test]
    public void LaysACapsuleAlongATiltedAxis()
    {
        // HalfHeight 5 and Radius 1 leave a segment reaching 4 from the middle. Pitched 45 degrees, the capsule's up
        // leans toward the world's Z. The point in the cap lies on that axis 4.53 from the middle.
        Vector3 inTheCapAlongTheTilt = new(0f, 3.2f, 3.2f);
        Vector3 straightAboveTheMiddle = new(0f, 4.9f, 0f);

        CapsuleRegion capsule = new()
        {
            HalfHeight = 5f,
            Radius = 1f,
            Rotation = Rotation.FromEulerAngles(45f, 0f, 0f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(capsule.IsInRegion(inTheCapAlongTheTilt), Is.True);
            Assert.That(capsule.IsInRegion(straightAboveTheMiddle), Is.False, "the upright capsule holds it");
        });
    }

    [Test]
    public void TurnsAHullAfterItsPlanesAreAdded()
    {
        // A beam 10 long along its own X from its origin, 2 tall and 4 wide. It lies on one side of its origin, so the
        // direction of a turn shows. A quarter turn right swings its length onto the world's Z, where it runs from 0
        // to -10.
        const int sampleCount = 10;

        Vector3 towardNegativeX = new(-1f, 0f, 0f);
        Vector3 towardPositiveX = new(1f, 0f, 0f);
        Vector3 towardNegativeY = new(0f, -1f, 0f);
        Vector3 towardPositiveY = new(0f, 1f, 0f);
        Vector3 towardNegativeZ = new(0f, 0f, -1f);
        Vector3 towardPositiveZ = new(0f, 0f, 1f);

        Plane backEnd = new(towardNegativeX, 0f);
        Plane frontEnd = new(towardPositiveX, 10f);
        Plane underside = new(towardNegativeY, 1f);
        Plane topside = new(towardPositiveY, 1f);
        Plane leftSide = new(towardNegativeZ, 2f);
        Plane rightSide = new(towardPositiveZ, 2f);

        ConvexHullRegion beam = new() { RandomSource = new Xoshiro128RandomSource(7) };
        beam.AddPlane(backEnd);
        beam.AddPlane(frontEnd);
        beam.AddPlane(underside);
        beam.AddPlane(topside);
        beam.AddPlane(leftSide);
        beam.AddPlane(rightSide);

        beam.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(beam.IsInRegion(new Vector3(0f, 0f, -5f)), Is.True, "along the world's Z once turned");
            Assert.That(beam.IsInRegion(new Vector3(5f, 0f, 0f)), Is.False, "where it would lie unturned");
            Assert.That(beam.IsInRegion(new Vector3(0f, 0f, 5f)), Is.False, "where a turn the other way would put it");

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Assert.That(beam.TryGetRandomPosition(out Vector3 sample), Is.True);
                Assert.That(beam.IsInRegion(sample), Is.True, $"sample {sampleIndex} at {sample} landed outside");
            }
        });
    }

    [Test]
    public void PointsATargetAndReadsItBack()
    {
        Vector3 vantagePosition = new(3f, 10f, 4f);

        PositionTarget vantage = new() { Position = vantagePosition };

        Assert.Multiple(() =>
        {
            Assert.That(vantage.TryGetPosition(out Vector3 position), Is.True);
            Assert.That(position, Is.EqualTo(vantagePosition));
        });
    }

    [Test]
    public void ReadsATransformFromAGameTargetThatGivesOnlyAPosition()
    {
        // The target answers a position away from the world's origin, so a default that dropped it would fail here. The
        // answer is compared as a whole transform, so the scale of one is held as well as the identity.
        Vector3 rallyPoint = new(3f, 10f, 4f);
        HostWaypointTarget rally = new(rallyPoint);
        Transform unturnedAtTheRallyPoint = new(rallyPoint, Rotation.Identity);

        Assert.Multiple(() =>
        {
            Assert.That(rally.TryGetTransform(out Transform transform), Is.True);
            Assert.That(transform, Is.EqualTo(unturnedAtTheRallyPoint));
        });
    }

    [Test]
    public void FailsTheTransformWhereAGameTargetFailsThePosition()
    {
        HostWaypointTarget lost = new(null);

        Assert.Multiple(() =>
        {
            Assert.That(lost.TryGetPosition(out _), Is.False);
            Assert.That(lost.TryGetTransform(out _), Is.False);
        });
    }

    [Test]
    public void BuildsACurveByHand()
    {
        Curve response = new();
        response.AddKeyframe(new CurveKeyframe(0f, 0f));
        response.AddKeyframe(new CurveKeyframe(1f, 2f));
        response.SetLinearTangents();

        Assert.That(response.Evaluate(0.5f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ReshapesACurveByHand()
    {
        // Raising the end and taking out the peak leaves a straight line from 0 to 4, once the tangents are set again.
        Curve response = new();
        response.AddKeyframe(new CurveKeyframe(0f, 0f));
        response.AddKeyframe(new CurveKeyframe(1f, 5f));
        response.AddKeyframe(new CurveKeyframe(2f, 2f));
        response.SetLinearTangents();

        bool raised = response.SetKeyframe(new CurveKeyframe(2f, 4f));
        bool removed = response.RemoveKeyframe(1f);
        response.SetLinearTangents();

        Assert.Multiple(() =>
        {
            Assert.That(raised, Is.True);
            Assert.That(removed, Is.True);
            Assert.That(response.Keyframes, Has.Count.EqualTo(2));
            Assert.That(response.Evaluate(1f), Is.EqualTo(2f).Within(0.0001f));
        });
    }

    /// <summary>
    ///     A game-defined target that overrides only TryGetPosition, answering the waypoint it was made with, or failing
    ///     when it was made with none.
    /// </summary>
    private sealed class HostWaypointTarget : TargetBase
    {
        private readonly Vector3? _waypoint;

        /// <summary>Creates a target that answers the given waypoint, or fails every ask when it is null.</summary>
        public HostWaypointTarget(Vector3? waypoint)
        {
            _waypoint = waypoint;
        }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            position = _waypoint ?? Vector3.Zero;
            return _waypoint.HasValue;
        }
    }
}
