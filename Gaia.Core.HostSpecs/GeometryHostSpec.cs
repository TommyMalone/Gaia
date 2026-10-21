using Gaia.Core.Blackboards;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Curves;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.HostSpecs;

/// <summary>Proves that game code can build regions, targets and curves and query them through the public surface alone.</summary>
/// <remarks>
///     A region is queried by the host, not by the library. The library fills no <see cref="Contact" /> list, so a
///     host tests each contact against a region itself and writes the ones inside to the blackboard entity list that a
///     configuration's <see cref="EntityExistsConsideration" /> reads.
/// </remarks>
[TestFixture]
public class GeometryHostSpec
{
    [Test]
    public void BuildsARegionAndTellsAPositionInsideItFromOneOutside()
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
        // A corridor 50 long that runs along the world's X from the origin is a rectangle reaching 25 along its
        // forward, turned a quarter turn about its middle. Both points answer the other way with the turn left out.
        Vector3 center = new(25f, 0f, 0f);
        Vector2 extents = new(2f, 25f);

        RectangleRegion corridor = new(center, extents) { Rotation = Rotation.FromEulerAngles(0f, 90f, 0f) };

        Assert.Multiple(() =>
        {
            Assert.That(corridor.IsInRegion(new Vector3(45f, 0f, 0f)), Is.True, "along the world's X once turned");
            Assert.That(corridor.IsInRegion(new Vector3(25f, 0f, 20f)), Is.False, "where it would lie unturned");
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
    public void TurnsAHandBuiltSector()
    {
        // A quarter turn right opens the sector along the world's X. (20, 0, 3) comes in 16.7 degrees left of its
        // forward, and (10, 0, 15) straight to its left, where the unturned sector would hold it straight ahead.
        Vector3 aheadOnceTurned = new(20f, 0f, 3f);
        Vector3 aheadUnturned = new(10f, 0f, 15f);

        SectorRegion fieldOfView = new()
        {
            Origin = new Vector3(10f, 0f, 0f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Angle = 60f,
            Range = 20f,
            HalfHeight = 1.5f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(fieldOfView.IsInRegion(aheadOnceTurned), Is.True, "ahead along the world's X once turned");
            Assert.That(fieldOfView.IsInRegion(aheadUnturned), Is.False, "where it would lie ahead unturned");
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
    public void FollowsATargetThatHasTurned()
    {
        // The sentry faces the world's X, so a box 5 ahead of it along its own forward lies 5 along X from it. Carried
        // by the sentry's position alone, the box would lie 5 along the world's Z instead.
        Vector3 aheadOfTheSentry = new(105f, 0f, 0f);
        Vector3 whereThePositionAloneWouldPutIt = new(100f, 0f, 5f);

        Contact sentry = new()
        {
            Name = "Sentry",
            Position = new Vector3(100f, 0f, 0f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
        };

        WorldBlackboard world = new();
        world.SetEntityList("Contacts", [sentry]);

        ByNameTarget sentryTarget = new()
        {
            DataStore = world,
            ListName = "Contacts",
            EntityName = "Sentry",
        };

        BoxRegion aheadOfTheTarget = new()
        {
            Origin = new Vector3(0f, 0f, 5f),
            Extents = new Vector3(1f, 1f, 1f),
            OriginTarget = sentryTarget,
        };

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheTarget.IsInRegion(aheadOfTheSentry), Is.True);
            Assert.That(aheadOfTheTarget.IsInRegion(whereThePositionAloneWouldPutIt), Is.False);
        });
    }

    [Test]
    public void KeepsItsOffsetFromATargetThatMovesAndTurns()
    {
        // The ball sits 10 ahead of the actor along the actor's forward. The actor then moves to (50, 0, 0) and turns a
        // quarter right, so its forward is the world's X and the ball's middle is at (60, 0, 0). Setting the ball's own
        // Origin 20 ahead moves the ball and not the actor. The radius is under 10, so a ball left on the actor holds
        // neither point.
        Vector3 tenAheadOfTheMovedActor = new(60f, 0f, 0f);
        Vector3 twentyAheadOfTheMovedActor = new(70f, 0f, 0f);
        Vector3 whereTheActorMovedTo = new(50f, 0f, 0f);

        Actor actor = new();
        SelfTarget self = new() { Self = actor };

        SphereRegion aheadOfTheActor = new()
        {
            Origin = new Vector3(0f, 0f, 10f),
            Radius = 1f,
            OriginTarget = self,
        };

        actor.Position = whereTheActorMovedTo;
        actor.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(tenAheadOfTheMovedActor), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(twentyAheadOfTheMovedActor), Is.False);
        });

        aheadOfTheActor.Origin = new Vector3(0f, 0f, 20f);

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(twentyAheadOfTheMovedActor), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(tenAheadOfTheMovedActor), Is.False);
            Assert.That(actor.Position, Is.EqualTo(whereTheActorMovedTo),
                "the shape's Origin leaves the actor as it was");
        });
    }

    [Test]
    public void KeepsItsOwnOrientationUnderAnAbsoluteRotation()
    {
        // The actor has turned a quarter right, so the box 3 ahead of it sits at (3, 0, 0) either way. Turned with the
        // actor, the box's length of 2 lies along the world's Z. With AbsoluteRotation it lies along the world's X, as
        // the box's own Rotation has it, so the two points answer the other way round.
        Vector3 middleOfTheBox = new(3f, 0f, 0f);
        Vector3 alongTheWorldsX = new(4.8f, 0f, 0f);
        Vector3 alongTheWorldsZ = new(3f, 0f, 1.8f);

        Actor actor = new() { Rotation = Rotation.FromEulerAngles(0f, 90f, 0f) };
        SelfTarget self = new() { Self = actor };

        BoxRegion aheadOfTheActor = new()
        {
            Origin = new Vector3(0f, 0f, 3f),
            Extents = new Vector3(2f, 1f, 0.5f),
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(middleOfTheBox), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(alongTheWorldsZ), Is.True, "turned with the actor");
            Assert.That(aheadOfTheActor.IsInRegion(alongTheWorldsX), Is.False);
        });

        aheadOfTheActor.AbsoluteRotation = true;

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(middleOfTheBox), Is.True, "Origin still swings with the actor");
            Assert.That(aheadOfTheActor.IsInRegion(alongTheWorldsX), Is.True, "keeping its own orientation");
            Assert.That(aheadOfTheActor.IsInRegion(alongTheWorldsZ), Is.False);
        });
    }

    [Test]
    public void KeepsItsOwnSizeUnderAnAbsoluteScale()
    {
        // The actor is scaled by 3, so the ball of radius 1 around it reaches 3 while it stretches with the actor, and
        // 1 once AbsoluteScale keeps its own size.
        Vector3 twoFromTheActor = new(2f, 0f, 0f);

        Actor actor = new() { Scale = new Vector3(3f, 3f, 3f) };
        SelfTarget self = new() { Self = actor };

        SphereRegion aroundTheActor = new()
        {
            Radius = 1f,
            OriginTarget = self,
        };

        Assert.That(aroundTheActor.IsInRegion(twoFromTheActor), Is.True, "stretched with the actor");

        aroundTheActor.AbsoluteScale = true;

        Assert.That(aroundTheActor.IsInRegion(twoFromTheActor), Is.False, "keeping its own size");
    }

    [Test]
    public void StretchesItsOffsetUnderAnAbsoluteScale()
    {
        // AbsoluteScale keeps only the ball's own size, so its offset of 2 ahead of the actor still stretches to 6
        // with the actor's scale of 3. The ball of radius 1 then holds 6.5 ahead and not 8, which it would hold
        // stretched to radius 3, nor 2.5, where its offset would put it if the actor's scale were dropped.
        Vector3 halfPastTheMiddle = new(0f, 0f, 6.5f);
        Vector3 twoPastTheMiddle = new(0f, 0f, 8f);
        Vector3 whereTheUnstretchedOffsetIs = new(0f, 0f, 2.5f);

        Actor actor = new() { Scale = new Vector3(3f, 3f, 3f) };
        SelfTarget self = new() { Self = actor };

        SphereRegion aheadOfTheActor = new()
        {
            Origin = new Vector3(0f, 0f, 2f),
            Radius = 1f,
            AbsoluteScale = true,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(halfPastTheMiddle), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(twoPastTheMiddle), Is.False, "keeping its own size");
            Assert.That(aheadOfTheActor.IsInRegion(whereTheUnstretchedOffsetIs), Is.False,
                "its offset stretched with the actor");
        });
    }

    [TestCase(3f, 3.0005f, 3.002f)]
    [TestCase(0.01f, 0.0105f, 0.012f)]
    public void HoldsAMillimeterPastItsBoundaryOnAScaledTarget(
        float actorsScale,
        float halfAMillimeterPast,
        float twoMillimetersPast)
    {
        // The ball of radius 1 stretches with the actor to radius actorsScale in the world. The tolerance is 1
        // millimeter in the world whatever the actor's scale, so half a millimeter past the surface counts as inside
        // and two millimeters past as outside. Divided by the ball's own Scale alone, the tolerance would stretch with
        // the actor to 3 millimeters on the larger actor and shrink to a hundredth of one on the smaller.
        Actor actor = new() { Scale = new Vector3(actorsScale, actorsScale, actorsScale) };
        SelfTarget self = new() { Self = actor };

        SphereRegion aroundTheActor = new()
        {
            Radius = 1f,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(aroundTheActor.IsInRegion(new Vector3(halfAMillimeterPast, 0f, 0f)), Is.True);
            Assert.That(aroundTheActor.IsInRegion(new Vector3(twoMillimetersPast, 0f, 0f)), Is.False);
        });
    }

    [Test]
    public void TurnsWithItsTargetUnderAnAbsoluteScale()
    {
        // AbsoluteScale keeps the box's own size and nothing else, so the box still turns with the actor. Turned a
        // quarter right with it, the box's length of 2 lies along the world's Z.
        Vector3 alongTheWorldsZ = new(0f, 0f, 1.8f);
        Vector3 alongTheWorldsX = new(1.8f, 0f, 0f);

        Actor actor = new() { Rotation = Rotation.FromEulerAngles(0f, 90f, 0f) };
        SelfTarget self = new() { Self = actor };

        BoxRegion onTheActor = new()
        {
            Extents = new Vector3(2f, 1f, 0.5f),
            AbsoluteScale = true,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(onTheActor.IsInRegion(alongTheWorldsZ), Is.True);
            Assert.That(onTheActor.IsInRegion(alongTheWorldsX), Is.False, "where the box lies with its own turn kept");
        });
    }

    [Test]
    public void StretchesWithItsTargetUnderAnAbsoluteRotation()
    {
        // AbsoluteRotation keeps the ball's own orientation and nothing else, so the ball of radius 1 still reaches 3
        // on an actor scaled by 3.
        Vector3 twoFromTheActor = new(2f, 0f, 0f);

        Actor actor = new() { Scale = new Vector3(3f, 3f, 3f) };
        SelfTarget self = new() { Self = actor };

        SphereRegion aroundTheActor = new()
        {
            Radius = 1f,
            AbsoluteRotation = true,
            OriginTarget = self,
        };

        Assert.That(aroundTheActor.IsInRegion(twoFromTheActor), Is.True);
    }

    [Test]
    public void HoldsAMillimeterPastItsBoundaryInTheWorldUnderAnAbsoluteRotation()
    {
        // The actor has turned a quarter right and is scaled by 3. The box keeps its own turn, so its extents of (2, 1,
        // 0.5) lie along the world's axes, and stretches with the actor to 6 along X and 1.5 along Z. Its tolerance is
        // 1 millimeter in the world, as a box stretched with its target holds, so half a millimeter past an end or a
        // side is inside and two millimeters past is outside. Turned with the actor, its length would lie along the
        // world's Z and hold the last point.
        Vector3 halfAMillimeterPastItsEnd = new(6.0005f, 0f, 0f);
        Vector3 twoMillimetersPastItsEnd = new(6.002f, 0f, 0f);
        Vector3 halfAMillimeterPastItsSide = new(0f, 0f, 1.5005f);
        Vector3 twoMillimetersPastItsSide = new(0f, 0f, 1.502f);
        Vector3 whereItWouldLieTurnedWithTheActor = new(0f, 0f, 5.9f);

        Actor actor = new()
        {
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Scale = new Vector3(3f, 3f, 3f),
        };
        SelfTarget self = new() { Self = actor };

        BoxRegion onTheActor = new()
        {
            Extents = new Vector3(2f, 1f, 0.5f),
            AbsoluteRotation = true,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(onTheActor.IsInRegion(halfAMillimeterPastItsEnd), Is.True);
            Assert.That(onTheActor.IsInRegion(twoMillimetersPastItsEnd), Is.False);
            Assert.That(onTheActor.IsInRegion(halfAMillimeterPastItsSide), Is.True);
            Assert.That(onTheActor.IsInRegion(twoMillimetersPastItsSide), Is.False);
            Assert.That(onTheActor.IsInRegion(whereItWouldLieTurnedWithTheActor), Is.False, "keeping its own turn");
        });
    }

    [Test]
    public void HoldsAMillimeterPastItsOwnBoundaryUnderAnAbsoluteScale()
    {
        // The actor has turned a quarter right and is scaled by 3. The box is turned a quarter right of its own inside
        // it, so it lies a half turn round in the world with its length of 2 along the world's X, where the actor's
        // turn alone would lay it along Z. It keeps its own size and its own tolerance, so half a millimeter past an
        // end or a side is inside and two millimeters past is outside. Stretched with the actor, it would hold the last
        // point.
        Vector3 halfAMillimeterPastItsEnd = new(2.0005f, 0f, 0f);
        Vector3 twoMillimetersPastItsEnd = new(2.002f, 0f, 0f);
        Vector3 halfAMillimeterPastItsSide = new(0f, 0f, 0.5005f);
        Vector3 twoMillimetersPastItsSide = new(0f, 0f, 0.502f);
        Vector3 whereItWouldReachStretchedWithTheActor = new(5.9f, 0f, 0f);

        Actor actor = new()
        {
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Scale = new Vector3(3f, 3f, 3f),
        };
        SelfTarget self = new() { Self = actor };

        BoxRegion onTheActor = new()
        {
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Extents = new Vector3(2f, 1f, 0.5f),
            AbsoluteScale = true,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(onTheActor.IsInRegion(halfAMillimeterPastItsEnd), Is.True);
            Assert.That(onTheActor.IsInRegion(twoMillimetersPastItsEnd), Is.False);
            Assert.That(onTheActor.IsInRegion(halfAMillimeterPastItsSide), Is.True);
            Assert.That(onTheActor.IsInRegion(twoMillimetersPastItsSide), Is.False);
            Assert.That(onTheActor.IsInRegion(whereItWouldReachStretchedWithTheActor), Is.False,
                "keeping its own size");
        });
    }

    [Test]
    public void KeepsItsOwnTurnAndSizeUnderBothAbsoluteFlags()
    {
        // The actor has turned a quarter right and is scaled by 3, so the box's offset of 3 ahead swings and stretches
        // to (9, 0, 0). Under both flags the box keeps its own turn and size there, its length of 2 along the world's X
        // and its depth of 0.5 along Z, and its own tolerance. Turned with the actor its length would lie along Z, and
        // stretched with it its depth would reach 1.5, either of which holds the last point.
        Vector3 halfAMillimeterPastItsEnd = new(11.0005f, 0f, 0f);
        Vector3 twoMillimetersPastItsEnd = new(11.002f, 0f, 0f);
        Vector3 halfAMillimeterPastItsSide = new(9f, 0f, 0.5005f);
        Vector3 twoMillimetersPastItsSide = new(9f, 0f, 0.502f);
        Vector3 whereItWouldReachTurnedOrStretched = new(9f, 0f, 1.4f);

        Actor actor = new()
        {
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Scale = new Vector3(3f, 3f, 3f),
        };
        SelfTarget self = new() { Self = actor };

        BoxRegion aheadOfTheActor = new()
        {
            Origin = new Vector3(0f, 0f, 3f),
            Extents = new Vector3(2f, 1f, 0.5f),
            AbsoluteRotation = true,
            AbsoluteScale = true,
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(aheadOfTheActor.IsInRegion(halfAMillimeterPastItsEnd), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(twoMillimetersPastItsEnd), Is.False);
            Assert.That(aheadOfTheActor.IsInRegion(halfAMillimeterPastItsSide), Is.True);
            Assert.That(aheadOfTheActor.IsInRegion(twoMillimetersPastItsSide), Is.False);
            Assert.That(aheadOfTheActor.IsInRegion(whereItWouldReachTurnedOrStretched), Is.False,
                "keeping its own turn and size");
        });
    }

    [Test]
    public void ComposesAnUnevenlyScaledTargetAsUnrealDoes()
    {
        // The actor is stretched to twice its size along the world's X, and the cube of extents 1 inside it is turned
        // 45 degrees. The composition stretches the cube along its own axes, into a box of extents (2, 1, 1) turned 45
        // degrees, where an exact composition would shear it. The first point lies 0.85 along the stretched box's right
        // and on its middle plane, inside it and outside the sheared one. The second lies 1.77 along its forward,
        // outside it and inside the sheared one.
        Vector3 insideTheStretchedCube = new(1.2f, 0f, -1.2f);
        Vector3 insideTheShearedCube = new(2.5f, 0f, 0f);

        Actor actor = new() { Scale = new Vector3(2f, 1f, 1f) };
        SelfTarget self = new() { Self = actor };

        BoxRegion turnedInsideTheActor = new()
        {
            Extents = new Vector3(1f, 1f, 1f),
            Rotation = Rotation.FromEulerAngles(0f, 45f, 0f),
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(turnedInsideTheActor.IsInRegion(insideTheStretchedCube), Is.True);
            Assert.That(turnedInsideTheActor.IsInRegion(insideTheShearedCube), Is.False);
        });
    }

    [Test]
    public void MultipliesItsOwnScaleByTheScaleOfItsTarget()
    {
        // The ball's Radius of 1 is doubled by its own Scale and tripled by the actor's, so it reaches 6 from where the
        // actor stands. A ball that kept only the actor's scale would reach 3, and one that kept only its own would
        // reach 2, so the point 5.9 ahead lies inside only the composed ball, and the point 6.1 ahead lies outside it.
        Vector3 insideTheComposedReach = new(0f, 0f, 5.9f);
        Vector3 pastTheComposedReach = new(0f, 0f, 6.1f);

        Actor actor = new() { Scale = new Vector3(3f, 3f, 3f) };
        SelfTarget self = new() { Self = actor };

        SphereRegion ballOnTheActor = new()
        {
            Radius = 1f,
            Scale = new Vector3(2f, 2f, 2f),
            OriginTarget = self,
        };

        Assert.Multiple(() =>
        {
            Assert.That(ballOnTheActor.IsInRegion(insideTheComposedReach), Is.True);
            Assert.That(ballOnTheActor.IsInRegion(pastTheComposedReach), Is.False);
        });
    }

    [Test]
    public void ContainsNothingOnATargetScaledToNothing()
    {
        // Each of the cube's planes faces along one of its axes. Scaled to nothing by the actor, a position far away
        // comes in with infinite coordinates, and the actor's own position with coordinates that are not a number.
        // Either way every plane's distance is not a number, which no plane rejects. The cube's own Scale is (1, 1, 1),
        // so only the scale composed with the actor's shows the zero.
        Vector3 farOutside = new(1000f, 1000f, 1000f);
        Vector3 whereTheActorStands = new(50f, 0f, 0f);

        Vector3 towardNegativeX = new(-1f, 0f, 0f);
        Vector3 towardPositiveX = new(1f, 0f, 0f);
        Vector3 towardNegativeY = new(0f, -1f, 0f);
        Vector3 towardPositiveY = new(0f, 1f, 0f);
        Vector3 towardNegativeZ = new(0f, 0f, -1f);
        Vector3 towardPositiveZ = new(0f, 0f, 1f);

        Plane leftWall = new(towardNegativeX, 1f);
        Plane rightWall = new(towardPositiveX, 1f);
        Plane floor = new(towardNegativeY, 1f);
        Plane ceiling = new(towardPositiveY, 1f);
        Plane nearWall = new(towardNegativeZ, 1f);
        Plane farWall = new(towardPositiveZ, 1f);

        Actor actor = new()
        {
            Position = whereTheActorStands,
            Scale = new Vector3(0f, 0f, 0f),
        };
        SelfTarget self = new() { Self = actor };

        ConvexHullRegion cube = new()
        {
            OriginTarget = self,
            RandomSource = new Xoshiro128RandomSource(7),
        };
        cube.AddPlane(leftWall);
        cube.AddPlane(rightWall);
        cube.AddPlane(floor);
        cube.AddPlane(ceiling);
        cube.AddPlane(nearWall);
        cube.AddPlane(farWall);

        Assert.Multiple(() =>
        {
            Assert.That(cube.IsInRegion(farOutside), Is.False);
            Assert.That(cube.IsInRegion(whereTheActorStands), Is.False);
            Assert.That(cube.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void MirrorsItsTurnWithAFlippedTarget()
    {
        // A 2D game turns its enemy around by a Scale of (-1, 1, 1). The triangle is turned to stand in the world's XY,
        // the screen, with its apex at its Origin and its opening along the world's +X. Flipped with the enemy, it
        // opens along -X, ahead of the flipped enemy. A composition that mirrored the triangle's offset and kept its
        // turn would leave it opening behind the enemy, along +X.
        Vector3 aheadOfTheFlippedEnemy = new(6f, 2f, 0f);
        Vector3 behindTheFlippedEnemy = new(14f, 2f, 0f);

        Contact enemy = new()
        {
            Name = "Enemy",
            Position = new Vector3(10f, 2f, 0f),
            Scale = new Vector3(-1f, 1f, 1f),
        };

        WorldBlackboard world = new();
        world.SetEntityList("Contacts", [enemy]);

        ByNameTarget enemyTarget = new()
        {
            DataStore = world,
            ListName = "Contacts",
            EntityName = "Enemy",
        };

        PolygonRegion viewCone = new()
        {
            Rotation = Rotation.FromEulerAngles(0f, 90f, 90f),
            OriginTarget = enemyTarget,
        };
        viewCone.AddCorner(new Vector2(0f, 0f));
        viewCone.AddCorner(new Vector2(2f, 5f));
        viewCone.AddCorner(new Vector2(-2f, 5f));

        Assert.Multiple(() =>
        {
            Assert.That(viewCone.IsInRegion(aheadOfTheFlippedEnemy), Is.True);
            Assert.That(viewCone.IsInRegion(behindTheFlippedEnemy), Is.False,
                "where the triangle opens with its turn kept unmirrored");
        });
    }

    [Test]
    public void LoadsTheCornersOfAGameDefinedFootprint()
    {
        // The footprint describes itself through DescribeFootprint and loads its corners through LoadFootprint, as the
        // shipped footprints do.
        const string region = """
                              <Region Type="Triangle">
                                  <Corner Position="(0, 0)"/>
                                  <Corner Position="(10, 0)"/>
                                  <Corner Position="(0, 10)"/>
                              </Region>
                              """;

        RegionBase loaded = LoadThroughAGameConsideration(region);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.InstanceOf<HostFootprintRegion>());
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 2f)), Is.True);
            Assert.That(loaded.IsInRegion(new Vector3(9f, 0f, 9f)), Is.False, "beyond the diagonal edge");
        });
    }

    [Test]
    public void LoadsTheTransformOfAGameDefinedShape()
    {
        // The ball describes itself through DescribeShape alone and writes no OnInit, so its Origin and Scale are the
        // base's reads. (11.5, 0, 0) comes in at 0.75 along its right and (12.5, 0, 0) at 1.25. With Scale unread,
        // (11.5, 0, 0) would come in at 1.5, and with Origin unread at 5.75.
        const string region = """<Region Type="UnitBall" Origin="(10, 0, 0)" Scale="(2, 2, 2)"/>""";

        RegionBase loaded = LoadThroughAGameConsideration(region);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.InstanceOf<HostUnitBallRegion>());
            Assert.That(loaded.IsInRegion(new Vector3(11.5f, 0f, 0f)), Is.True);
            Assert.That(loaded.IsInRegion(new Vector3(12.5f, 0f, 0f)), Is.False);
        });
    }

    [Test]
    public void ReadsBackThePositionAPositionTargetWasGiven()
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
    ///     Loads a brain whose one option holds a game consideration with the given Region element, through factories
    ///     that register the game's two shapes, and hands back the region the consideration built.
    /// </summary>
    /// <remarks>
    ///     A host builds a region from a file only inside a component of its own, since the creation data's constructor
    ///     is internal, so the consideration is what puts the region under a loaded parent.
    /// </remarks>
    private static RegionBase LoadThroughAGameConsideration(string regionElement)
    {
        ListLogSink logSink = new();
        List<string> selections = new();

        FactorySet factories = new();
        factories.Regions.Register("Triangle", () => new HostFootprintRegion(), HostFootprintRegion.Description);
        factories.Regions.Register("UnitBall", () => new HostUnitBallRegion(), HostUnitBallRegion.Description);
        factories.Considerations.Register("InRegion", () => new HostRegionConsideration(),
            HostRegionConsideration.Description);
        factories.Actions.Register("Record", () => new HostRecordAction(selections), HostRecordAction.Description);

        // A master is supplied so the loader reports no rolled seed into the sink the test reads.
        GaiaLoader loader = new(factories, new ManualTimeSource(), new FixedRandomSource(0f), logSink);

        string configurationText = $"""
                                    <Brain>
                                        <Reasoner Type="RuleBased">
                                            <Option Type="ConsiderationAndAction" Name="Hold">
                                                <Consideration Type="InRegion">
                                    {regionElement}
                                                </Consideration>
                                                <Action Type="Record" Name="Hold"/>
                                            </Option>
                                        </Reasoner>
                                    </Brain>
                                    """;

        bool isLoaded = loader.TryLoadBrain(configurationText, "regions.xml", out Brain? brain);

        Assert.That(isLoaded, Is.True);
        Assert.That(logSink.Entries, Is.Empty);

        return ((HostRegionConsideration)brain!.RootReasoner!.Options[0].Considerations[0]).Region!;
    }

    /// <summary>A game-defined consideration that builds one region from its Region child and reports neutral values.</summary>
    private sealed class HostRegionConsideration : ConsiderationBase
    {
        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.Considerations,
            "Holds one region the game asks.");

        /// <summary>Describes the Region child OnInit builds.</summary>
        private static readonly SlotDescription RegionSlot = Builder.Slot("Region", ConceptualAbstraction.Regions, 1,
            1);

        /// <summary>Describes the consideration for the tests that register it.</summary>
        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>The region built from the Region child.</summary>
        public RegionBase? Region { get; private set; }

        /// <inheritdoc />
        public override void Calculate()
        {
            Weights = WeightValues.Neutral;
        }

        /// <summary>Builds the Region child, which the factory has already held to one.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            foreach (SpecificationNode child in creationData.Node.Children)
            {
                if (!creationData.Factories.Regions.TryCreate(creationData.ForChild(child), out RegionBase? region))
                {
                    return false;
                }

                Region = region;
            }

            return true;
        }
    }

    /// <summary>A game-defined footprint, which describes and loads its corners through the base.</summary>
    private sealed class HostFootprintRegion : PolygonRegionBase
    {
        /// <summary>Describes the footprint for the tests that register it.</summary>
        internal static readonly ComponentDescription Description =
            DescribeFootprint("A footprint of the game's own, which ignores height.").Build();

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return IsInFootprint(InPlane(local, out _));
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            if (!TryGetRandomFootprintPoint(out Vector2 point))
            {
                local = Vector3.Zero;

                return false;
            }

            local = OutOfPlane(point, 0f);

            return true;
        }
    }

    /// <summary>A game-defined ball of radius 1 in its own coordinates, which describes itself through the base alone.</summary>
    private sealed class HostUnitBallRegion : RegionBase
    {
        /// <summary>Describes the ball for the tests that register it, with nothing of its own.</summary>
        internal static readonly ComponentDescription Description = DescribeShape("A ball of radius 1.").Build();

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            return local.SquaredMagnitude <= 1f;
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector3 reach = new(1f, 1f, 1f);

            return TryRejectionSample(Vector3.Zero - reach, reach, out local);
        }
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
