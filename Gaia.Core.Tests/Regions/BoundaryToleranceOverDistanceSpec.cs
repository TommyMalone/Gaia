using Gaia.Core.Regions;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>
///     Covers how far from the world's origin <see cref="RegionBase.BoundaryTolerance" /> holds a shape's boundary,
///     at fixed places worked out in float, so a change that lets a point there drift past the tolerance fails here.
/// </summary>
/// <remarks>
///     <para>
///         Each shape reaches less than 500 meters from its own origin, the size the tolerance's remarks give its reach
///         for. Each test places two or three of a box, a ball, a footprint and a rectangle at one distance and carries
///         points exactly on their boundaries out to the world through <see cref="Transform.ToWorld(Vector3)" />. Every
///         one of them must count as inside, and a point 2 millimeters out from the boundary in the world must count as
///         outside.
///     </para>
///     <para>
///         A float's step doubles with each doubling of the distance from the world's origin, and the most a point can
///         move on its way out to the world and back grows with it. Each test's comment gives the step there and how far
///         its worst point comes back past the boundary. A smaller tolerance, or a transform that rounds a point there
///         past the tolerance, fails the test.
///     </para>
///     <para>
///         Each test also brings every point back through the same placement and holds the worst of them under about one
///         and a half times how far it comes back today, so a transform that loses precision fails here before any point
///         passes the tolerance. Rewriting the transform's arithmetic in ways that are equal in exact arithmetic, adding a
///         dot product's terms in another order, fusing its multiplies, or writing the turn's diagonal another way, moved
///         these worst points up by at most 0.015 millimeters on October 11, 2026, where taking each dot product before
///         subtracting the origin doubled the turned ones. A test that fails only its limit after such a rewrite is
///         measured again, and its limit and comment brought up to date.
///     </para>
/// </remarks>
[TestFixture]
public class BoundaryToleranceOverDistanceSpec
{
    /// <summary>
    ///     The turn <see cref="Rotation.FromEulerAngles" /> gives for (20, 35, 10),
    ///     <see cref="RegionSpecHelpers.AwkwardTurn" />, written as its four components so the tests do not depend on the
    ///     platform's sine.
    /// </summary>
    private static readonly Rotation Turned = new(0.19079109f, 0.28057653f, 0.029840775f, 0.9402048f);

    /// <summary>
    ///     The box's extents, about 401 meters to a corner. Its X and Z extents are floats whose last bits lie below a
    ///     float's step 32 kilometers out, chosen so the box's faces along X and Z round outward there on their way out to the
    ///     world, by about 0.7 millimeters.
    /// </summary>
    private static readonly Vector3 BoxExtents = new(320.3704f, 25.13f, 240.6106f);

    /// <summary>
    ///     A fifteenth of the ball's radius, about 29.9 meters, the radius about 448.5 meters. Fifteen times it, and
    ///     every other whole number of it used here, is exactly a float, so each point below lies exactly on the ball's
    ///     surface. Its last bits lie below a float's step 16 kilometers out, so those points round on their way out to the
    ///     world there.
    /// </summary>
    private const float BallUnit = 29.9000244140625f;

    /// <summary>
    ///     A quarter of the footprint's width along its own X, about 160 meters, chosen as BallUnit is, so the points
    ///     below lie exactly on the footprint's slanted edge and round on their way out to the world.
    /// </summary>
    private const float FootprintUnit = 159.9000244140625f;

    /// <summary>The box's eight corners, the middle of each of its six faces, and four points elsewhere on its faces.</summary>
    private static readonly Vector3[] BoxBoundary = BoxBoundaryPoints(BoxExtents.X, BoxExtents.Y, BoxExtents.Z);

    /// <summary>The middle of each of the box's six faces, with the way out of it.</summary>
    private static readonly (Vector3 OnTheBoundary, Vector3 Outward)[] BoxWaysOut =
    {
        (new Vector3(BoxExtents.X, 0f, 0f), new Vector3(1f, 0f, 0f)),
        (new Vector3(-BoxExtents.X, 0f, 0f), new Vector3(-1f, 0f, 0f)),
        (new Vector3(0f, BoxExtents.Y, 0f), new Vector3(0f, 1f, 0f)),
        (new Vector3(0f, -BoxExtents.Y, 0f), new Vector3(0f, -1f, 0f)),
        (new Vector3(0f, 0f, BoxExtents.Z), new Vector3(0f, 0f, 1f)),
        (new Vector3(0f, 0f, -BoxExtents.Z), new Vector3(0f, 0f, -1f)),
    };

    /// <summary>
    ///     Points of the ball's surface where its axes meet it and in between, each made of three whole numbers of
    ///     BallUnit whose squares add to 15 squared.
    /// </summary>
    private static readonly Vector3[] BallBoundary =
    {
        new(15f * BallUnit, 0f, 0f), new(0f, 15f * BallUnit, 0f), new(0f, 0f, -15f * BallUnit),
        new(9f * BallUnit, 12f * BallUnit, 0f), new(0f, -9f * BallUnit, 12f * BallUnit),
        new(-12f * BallUnit, 0f, -9f * BallUnit), new(5f * BallUnit, 10f * BallUnit, 10f * BallUnit),
        new(-10f * BallUnit, 5f * BallUnit, -10f * BallUnit), new(10f * BallUnit, -10f * BallUnit, 5f * BallUnit),
        new(2f * BallUnit, -11f * BallUnit, 10f * BallUnit), new(-14f * BallUnit, 5f * BallUnit, 2f * BallUnit),
        new(11f * BallUnit, 2f * BallUnit, -10f * BallUnit),
    };

    /// <summary>Five points of the ball's surface, with the way out of it there.</summary>
    private static readonly (Vector3 OnTheBoundary, Vector3 Outward)[] BallWaysOut =
    {
        (new Vector3(15f * BallUnit, 0f, 0f), new Vector3(1f, 0f, 0f)),
        (new Vector3(0f, 15f * BallUnit, 0f), new Vector3(0f, 1f, 0f)),
        (new Vector3(9f * BallUnit, 12f * BallUnit, 0f), new Vector3(9f, 12f, 0f) / 15f),
        (new Vector3(-12f * BallUnit, 0f, -9f * BallUnit), new Vector3(-12f, 0f, -9f) / 15f),
        (new Vector3(-14f * BallUnit, 5f * BallUnit, 2f * BallUnit), new Vector3(-14f, 5f, 2f) / 15f),
    };

    /// <summary>The footprint's three corners, in its own coordinates, where the plane's Y is local Z.</summary>
    private static readonly Vector3[] FootprintCorners =
    {
        new(-2f * FootprintUnit, 0f, -1.5f * FootprintUnit), new(2f * FootprintUnit, 0f, -1.5f * FootprintUnit),
        new(-2f * FootprintUnit, 0f, 1.5f * FootprintUnit),
    };

    /// <summary>
    ///     Points of the footprint's two edges along its own axes, away from its corners, in the shape's own coordinates,
    ///     where the plane's Y is local Z.
    /// </summary>
    private static readonly Vector3[] FootprintEdgesAlongItsAxes =
    {
        new(-FootprintUnit, 0f, -1.5f * FootprintUnit), new(0f, 0f, -1.5f * FootprintUnit),
        new(FootprintUnit, 0f, -1.5f * FootprintUnit), new(-2f * FootprintUnit, 0f, -0.75f * FootprintUnit),
        new(-2f * FootprintUnit, 0f, 0f), new(-2f * FootprintUnit, 0f, 0.75f * FootprintUnit),
    };

    /// <summary>A quarter and three quarters of the way along the footprint's slanted edge.</summary>
    private static readonly Vector3[] FootprintSlantedEdge =
    {
        new(FootprintUnit, 0f, -0.75f * FootprintUnit), new(-FootprintUnit, 0f, 0.75f * FootprintUnit),
    };

    /// <summary>A point of each of the footprint's two edges along its own axes, with the way out of it there.</summary>
    private static readonly (Vector3 OnTheBoundary, Vector3 Outward)[] FootprintWaysOutAlongItsAxes =
    {
        (new Vector3(-FootprintUnit, 0f, -1.5f * FootprintUnit), new Vector3(0f, 0f, -1f)),
        (new Vector3(-2f * FootprintUnit, 0f, 0.75f * FootprintUnit), new Vector3(-1f, 0f, 0f)),
    };

    /// <summary>
    ///     The rectangle's four corners, the middle of each of its four edges, and two points elsewhere on them, its half
    ///     width and half depth the box's X and Z extents, in its own coordinates, where the plane's Y is local Z.
    /// </summary>
    private static readonly Vector3[] RectangleBoundary =
    {
        new(-BoxExtents.X, 0f, -BoxExtents.Z), new(BoxExtents.X, 0f, -BoxExtents.Z),
        new(-BoxExtents.X, 0f, BoxExtents.Z),
        new(BoxExtents.X, 0f, BoxExtents.Z), new(BoxExtents.X, 0f, 0f), new(-BoxExtents.X, 0f, 0f),
        new(0f, 0f, BoxExtents.Z), new(0f, 0f, -BoxExtents.Z), new(BoxExtents.X, 0f, -153.9f),
        new(-87.3f, 0f, BoxExtents.Z),
    };

    /// <summary>The middle of each of the rectangle's four edges, with the way out of it.</summary>
    private static readonly (Vector3 OnTheBoundary, Vector3 Outward)[] RectangleWaysOut =
    {
        (new Vector3(BoxExtents.X, 0f, 0f), new Vector3(1f, 0f, 0f)),
        (new Vector3(-BoxExtents.X, 0f, 0f), new Vector3(-1f, 0f, 0f)),
        (new Vector3(0f, 0f, BoxExtents.Z), new Vector3(0f, 0f, 1f)),
        (new Vector3(0f, 0f, -BoxExtents.Z), new Vector3(0f, 0f, -1f)),
    };

    /// <summary>The two points of the footprint's slanted edge, with the way out of it, square to the edge.</summary>
    private static readonly (Vector3 OnTheBoundary, Vector3 Outward)[] FootprintWaysOutOfTheSlantedEdge =
    {
        (new Vector3(FootprintUnit, 0f, -0.75f * FootprintUnit), new Vector3(0.6f, 0f, 0.8f)),
        (new Vector3(-FootprintUnit, 0f, 0.75f * FootprintUnit), new Vector3(0.6f, 0f, 0.8f)),
    };

    [Test]
    public void HoldsATurnedBoundaryAHundredMetersFromTheWorldsOrigin()
    {
        // About 100 meters out, with every coordinate under about 470 meters, a float's step is about 0.03 mm. The
        // worst point here comes back 0.03 mm past its boundary, leaving almost all of the 1 mm tolerance, and is held
        // under 0.05 mm.
        AssertEveryShapeHoldsItsBoundary(new Vector3(70.37f, 5.3f, -70.81f), Turned, 0.05);
    }

    [Test]
    public void HoldsATurnedBoundaryAKilometerFromTheWorldsOrigin()
    {
        // About 1 kilometer out, with every coordinate under about 1.1 kilometers, a float's step is about 0.12 mm. The
        // worst point here comes back 0.06 mm past its boundary, 0.94 mm inside the tolerance, and is held under
        // 0.10 mm.
        AssertEveryShapeHoldsItsBoundary(new Vector3(707.37f, 10.3f, -707.81f), Turned, 0.10);
    }

    [Test]
    public void HoldsATurnedBoundaryFourKilometersFromTheWorldsOrigin()
    {
        // About 4 kilometers out, with every coordinate under about 3.2 kilometers, a float's step is about 0.24 mm.
        // The worst point here comes back 0.23 mm past its boundary, 0.77 mm inside the tolerance, and is held under
        // 0.34 mm.
        AssertEveryShapeHoldsItsBoundary(new Vector3(2828.37f, 15.3f, -2828.81f), Turned, 0.34);
    }

    [Test]
    public void HoldsATurnedBoundaryJustInsideEightKilometersFromTheWorldsOrigin()
    {
        // About 7.8 kilometers out, with every coordinate under 8,192 meters, a float's step is about 0.49 mm. The
        // worst point here comes back 0.41 mm past its boundary, 0.59 mm inside the tolerance, and is held under
        // 0.62 mm. Past 8,192 meters the step doubles to about 1 mm, and a point of a turned shape can come back past
        // the tolerance.
        AssertEveryShapeHoldsItsBoundary(new Vector3(5500.37f, 20.3f, -5500.81f), Turned, 0.62);
    }

    [Test]
    public void HoldsARoundOrSlantedBoundaryUnturnedSixteenKilometersFromTheWorldsOrigin()
    {
        // About 16 kilometers out, with every coordinate under 16,384 meters, a float's step is about 0.98 mm.
        // Unturned, each coordinate rounds by at most half a step, and a round or slanted boundary, or a footprint's
        // corner, takes the rounding of two or three coordinates together. The worst point here comes back 0.39 mm past
        // its boundary, 0.61 mm inside the tolerance, and is held under 0.59 mm. Past 16,384 meters the step doubles to
        // about 2 mm, and a round or slanted boundary, or a footprint's corner, can come back past the tolerance.
        Vector3 origin = new(11300.37f, 25.3f, -11300.81f);
        const double limit = 0.59;

        Assert.Multiple(() =>
        {
            AssertItsBoundaryHolds(Ball(), PastTheBall, origin, Rotation.Identity, BallBoundary, BallWaysOut, limit);
            AssertItsBoundaryHolds(Footprint(), PastTheFootprint, origin, Rotation.Identity,
                FootprintCorners.Concat(FootprintEdgesAlongItsAxes).Concat(FootprintSlantedEdge),
                FootprintWaysOutAlongItsAxes.Concat(FootprintWaysOutOfTheSlantedEdge), limit);
        });
    }

    [Test]
    public void HoldsAFaceAlongItsAxesUnturnedThirtyTwoKilometersFromTheWorldsOrigin()
    {
        // About 32 kilometers out, with every coordinate under 32,768 meters, a float's step is about 1.95 mm.
        // Unturned, each coordinate rounds by at most half a step, 0.98 mm. A box and a rectangle compare one axis at a
        // time and take each coordinate's rounding alone, so they hold their whole boundaries as far as 32,768 meters
        // and no farther, and so does a footprint's edge along its axes, away from its corners. At a footprint's corner
        // the rounding of two coordinates adds up, to as much as about 1.4 mm, so its corners are left out here, and a
        // footprint holds only to about 16 kilometers. The worst point here comes back 0.73 mm past its boundary,
        // 0.27 mm inside the tolerance, and is held under 0.9 mm, since one and a half times it would pass the
        // tolerance. A point 2 mm out along X or Z is about one step out here, and comes back at least 1.5 mm out.
        Vector3 origin = new(22600.37f, 30.3f, -22600.81f);
        const double limit = 0.9;

        Assert.Multiple(() =>
        {
            AssertItsBoundaryHolds(Box(), PastTheBox, origin, Rotation.Identity, BoxBoundary, BoxWaysOut, limit);
            AssertItsBoundaryHolds(Rectangle(), PastTheRectangle, origin, Rotation.Identity, RectangleBoundary,
                RectangleWaysOut, limit);
            AssertItsBoundaryHolds(Footprint(), PastTheFootprint, origin, Rotation.Identity, FootprintEdgesAlongItsAxes,
                FootprintWaysOutAlongItsAxes, limit);
        });
    }

    /// <summary>The box's points on its boundary, built from its extents.</summary>
    private static Vector3[] BoxBoundaryPoints(float x, float y, float z)
    {
        return new Vector3[]
        {
            new(-x, -y, -z), new(x, -y, -z), new(-x, y, -z), new(x, y, -z), new(-x, -y, z), new(x, -y, z),
            new(-x, y, z), new(x, y, z), new(x, 0f, 0f), new(-x, 0f, 0f), new(0f, y, 0f), new(0f, -y, 0f),
            new(0f, 0f, z), new(0f, 0f, -z), new(x, 11.7f, -153.9f), new(-87.3f, y, 201.6f),
            new(233.1f, -18.4f, -z), new(-x, -7.25f, 66.6f),
        };
    }

    private static BoxRegion Box()
    {
        return new BoxRegion { Extents = BoxExtents };
    }

    private static SphereRegion Ball()
    {
        return new SphereRegion { Radius = 15f * BallUnit };
    }

    /// <summary>
    ///     A right triangle whose two short edges lie along its own axes and whose slanted edge passes through its own
    ///     origin, each of its corners about 400 meters from that origin.
    /// </summary>
    private static PolygonRegion Footprint()
    {
        PolygonRegion footprint = new();

        footprint.AddCorner(new Vector2(-2f * FootprintUnit, -1.5f * FootprintUnit));
        footprint.AddCorner(new Vector2(2f * FootprintUnit, -1.5f * FootprintUnit));
        footprint.AddCorner(new Vector2(-2f * FootprintUnit, 1.5f * FootprintUnit));

        return footprint;
    }

    /// <summary>A rectangle with the box's X and Z extents as its half width and half depth.</summary>
    private static RectangleRegion Rectangle()
    {
        return new RectangleRegion { Extents = new Vector2(BoxExtents.X, BoxExtents.Z) };
    }

    /// <summary>How far a point in the box's own coordinates lies past its boundary along any one axis, in meters.</summary>
    private static double PastTheBox(Vector3 local)
    {
        double alongX = double.Abs(local.X) - BoxExtents.X;
        double alongY = double.Abs(local.Y) - BoxExtents.Y;
        double alongZ = double.Abs(local.Z) - BoxExtents.Z;

        return double.Max(double.Max(alongX, alongY), alongZ);
    }

    /// <summary>How far a point in the ball's own coordinates lies past its surface, in meters.</summary>
    private static double PastTheBall(Vector3 local)
    {
        double x = local.X;
        double y = local.Y;
        double z = local.Z;

        return double.Sqrt(x * x + y * y + z * z) - 15.0 * BallUnit;
    }

    /// <summary>How far a point in the rectangle's own coordinates lies past its boundary along either of its axes, in meters.</summary>
    private static double PastTheRectangle(Vector3 local)
    {
        return double.Max(double.Abs(local.X) - BoxExtents.X, double.Abs(local.Z) - BoxExtents.Z);
    }

    /// <summary>
    ///     How far a point in the footprint's own coordinates lies past its boundary, in meters. Outside, it is the
    ///     point's distance from the nearest point of the nearest edge, as the footprint measures it, and inside, that
    ///     distance taken as less than 0.
    /// </summary>
    private static double PastTheFootprint(Vector3 local)
    {
        (double X, double Z)[] corners =
        {
            (-2.0 * FootprintUnit, -1.5 * FootprintUnit), (2.0 * FootprintUnit, -1.5 * FootprintUnit),
            (-2.0 * FootprintUnit, 1.5 * FootprintUnit),
        };
        double nearest = double.MaxValue;
        bool isInside = true;

        for (int i = 0; i < corners.Length; i++)
        {
            (double startX, double startZ) = corners[i];
            (double endX, double endZ) = corners[(i + 1) % corners.Length];
            double edgeX = endX - startX;
            double edgeZ = endZ - startZ;
            double pointX = local.X - startX;
            double pointZ = local.Z - startZ;
            double along = Math.Clamp((pointX * edgeX + pointZ * edgeZ) / (edgeX * edgeX + edgeZ * edgeZ), 0.0, 1.0);
            double offX = pointX - along * edgeX;
            double offZ = pointZ - along * edgeZ;

            // The corners run counterclockwise, so the inside lies to the left of every edge.
            isInside &= edgeX * pointZ - edgeZ * pointX >= 0.0;
            nearest = double.Min(nearest, double.Sqrt(offX * offX + offZ * offZ));
        }

        return isInside ? -nearest : nearest;
    }

    /// <summary>
    ///     Asserts that the box, the ball and the footprint each hold their boundary at the place and turn given, their
    ///     worst points within the limit.
    /// </summary>
    private static void AssertEveryShapeHoldsItsBoundary(Vector3 origin, Rotation rotation, double limit)
    {
        Assert.Multiple(() =>
        {
            AssertItsBoundaryHolds(Box(), PastTheBox, origin, rotation, BoxBoundary, BoxWaysOut, limit);
            AssertItsBoundaryHolds(Ball(), PastTheBall, origin, rotation, BallBoundary, BallWaysOut, limit);
            AssertItsBoundaryHolds(Footprint(), PastTheFootprint, origin, rotation,
                FootprintCorners.Concat(FootprintEdgesAlongItsAxes).Concat(FootprintSlantedEdge),
                FootprintWaysOutAlongItsAxes.Concat(FootprintWaysOutOfTheSlantedEdge), limit);
        });
    }

    /// <summary>
    ///     Places the region, carries each point of its boundary out to the world, and asserts that it is inside, that
    ///     brought back through the same placement the worst of them lies no farther past the boundary than the limit, and
    ///     that the position 2 millimeters out in the world from each point given with a way out is outside.
    /// </summary>
    /// <param name="region">The region to place.</param>
    /// <param name="pastItsBoundary">How far a point in the region's own coordinates lies past its boundary, in meters.</param>
    /// <param name="origin">Where to place it.</param>
    /// <param name="rotation">How to turn it.</param>
    /// <param name="onTheBoundary">Points exactly on its boundary, in its own coordinates.</param>
    /// <param name="waysOut">
    ///     Points on its boundary, each with the way out of the region from it, one unit long and square to
    ///     the boundary there, in its own coordinates.
    /// </param>
    /// <param name="limit">How far past the boundary, in millimeters, the worst point may come back.</param>
    private static void AssertItsBoundaryHolds(
        RegionBase region,
        Func<Vector3, double> pastItsBoundary,
        Vector3 origin,
        Rotation rotation,
        IEnumerable<Vector3> onTheBoundary,
        IEnumerable<(Vector3 OnTheBoundary, Vector3 Outward)> waysOut,
        double limit)
    {
        region.Origin = origin;
        region.Rotation = rotation;

        Transform placement = new(origin, rotation);
        Transform turn = new(Vector3.Zero, rotation);
        string shape = region.GetType().Name;
        double worst = double.MinValue;

        foreach (Vector3 point in onTheBoundary)
        {
            Vector3 inTheWorld = placement.ToWorld(point);

            Assert.That(region.IsInRegion(inTheWorld), Is.True,
                $"{shape}, {point} is on the boundary, carried out to {inTheWorld}");

            worst = double.Max(worst, pastItsBoundary(placement.ToLocal(inTheWorld)));
        }

        Assert.That(worst * 1000.0, Is.LessThanOrEqualTo(limit),
            $"{shape}, its worst point comes back {worst * 1000.0:F3} mm past its boundary, over the limit of {limit} mm");

        foreach ((Vector3 point, Vector3 outward) in waysOut)
        {
            Vector3 twoMillimetersOut = placement.ToWorld(point) + turn.ToWorld(outward) * 0.002f;

            Assert.That(region.IsInRegion(twoMillimetersOut), Is.False,
                $"{shape}, two millimeters out from {point}, at {twoMillimetersOut}");
        }
    }
}
