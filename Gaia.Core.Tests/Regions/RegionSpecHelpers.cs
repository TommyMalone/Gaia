using Gaia.Core.Regions;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>The asserts and builders the region specs share.</summary>
internal static class RegionSpecHelpers
{
    /// <summary>How many random positions the sampling asserts draw.</summary>
    internal const int SampleCount = 50;

    /// <summary>
    ///     Where the boundary tests place a shape, about 5.5 kilometers from the world's origin, where a float's step is
    ///     half a millimeter. A point worked out onto a turned shape's boundary there comes back into the shape's own
    ///     coordinates up to about 0.7 millimeters to either side of it, so about half of them come back outside it.
    /// </summary>
    internal static readonly Vector3 FarFromTheWorldsOrigin = new(4321.09f, 0f, -3456.78f);

    /// <summary>The turn the boundary tests give a shape, so that none of its own axes lies along one of the world's.</summary>
    internal static readonly Rotation AwkwardTurn = Rotation.FromEulerAngles(20f, 35f, 10f);

    /// <summary>
    ///     The six corners of the L shaped footprint and the middle of each of its six edges, every one of them on its
    ///     boundary.
    /// </summary>
    internal static readonly Vector2[] LShapedFootprintBoundaryPoints =
    {
        new(0f, 0f), new(10f, 0f), new(10f, 5f), new(5f, 5f), new(5f, 10f), new(0f, 10f),
        new(5f, 0f), new(10f, 2.5f), new(7.5f, 5f), new(5f, 7.5f), new(2.5f, 10f), new(0f, 5f),
    };

    /// <summary>
    ///     Draws SampleCount random positions from the region and asserts that each lands inside, since that is the only
    ///     promise a random position makes.
    /// </summary>
    internal static void AssertEverySampleLandsInside(RegionBase region)
    {
        for (int sampleIndex = 0; sampleIndex < SampleCount; sampleIndex++)
        {
            Assert.That(region.TryGetRandomPosition(out Vector3 position), Is.True,
                $"sample {sampleIndex} was not found");
            Assert.That(region.IsInRegion(position), Is.True, $"sample {sampleIndex} at {position} landed outside");
        }
    }

    /// <summary>
    ///     Places the region at FarFromTheWorldsOrigin, turned by AwkwardTurn, works out where each point of its boundary
    ///     lands in the world, and asserts that each of those positions is inside.
    /// </summary>
    /// <param name="region">The region to place, which is left where this puts it.</param>
    /// <param name="boundary">Points on the region's boundary, in its own coordinates.</param>
    internal static void AssertItsBoundaryHoldsFarFromTheWorldsOrigin(RegionBase region, IEnumerable<Vector3> boundary)
    {
        region.Origin = FarFromTheWorldsOrigin;
        region.Rotation = AwkwardTurn;

        Transform placement = new(region.Origin, region.Rotation, region.Scale);

        Assert.Multiple(() =>
        {
            foreach (Vector3 onTheBoundary in boundary)
            {
                Vector3 inTheWorld = placement.ToWorld(onTheBoundary);

                Assert.That(region.IsInRegion(inTheWorld), Is.True,
                    $"{onTheBoundary} is on the boundary, worked out to {inTheWorld}");
            }
        });
    }

    /// <summary>
    ///     Asserts that the region holds the position half a millimeter out from a point of its boundary and not the one
    ///     two millimeters out, where the region stands.
    /// </summary>
    /// <param name="region">The region, standing near the world's origin, where a float's step is far under 1 mm.</param>
    /// <param name="onTheBoundary">A point on the region's boundary, in its own coordinates.</param>
    /// <param name="outward">The way out of the region from that point, in its own coordinates, one unit long.</param>
    internal static void AssertItHoldsHalfAMillimeterOutButNotTwo(
        RegionBase region,
        Vector3 onTheBoundary,
        Vector3 outward)
    {
        Transform placement = new(region.Origin, region.Rotation, region.Scale);
        Vector3 halfAMillimeterOut = placement.ToWorld(onTheBoundary + outward * 0.0005f);
        Vector3 twoMillimetersOut = placement.ToWorld(onTheBoundary + outward * 0.002f);

        Assert.That(region.IsInRegion(halfAMillimeterOut), Is.True, $"half a millimeter out from {onTheBoundary}");
        Assert.That(region.IsInRegion(twoMillimetersOut), Is.False, $"two millimeters out from {onTheBoundary}");
    }

    /// <summary>
    ///     Asserts that the region holds the position half a millimeter out in the world from a point of its boundary and
    ///     not the one two millimeters out, whatever its Scale, since the tolerance is a length in the world.
    /// </summary>
    /// <param name="region">The region, standing near the world's origin, where a float's step is far under 1 mm.</param>
    /// <param name="onTheBoundary">A point on the region's boundary, in its own coordinates.</param>
    /// <param name="outward">
    ///     The way out of the region from that point, along one of its own axes and square to the boundary
    ///     there, which it stays however the axes are scaled.
    /// </param>
    internal static void AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(
        RegionBase region,
        Vector3 onTheBoundary,
        Vector3 outward)
    {
        Transform placement = new(region.Origin, region.Rotation, region.Scale);
        Vector3 boundaryInTheWorld = placement.ToWorld(onTheBoundary);
        Vector3 outwardInTheWorld = placement.ToWorld(onTheBoundary + outward) - boundaryInTheWorld;
        Vector3 wayOut = outwardInTheWorld / outwardInTheWorld.Magnitude;
        Vector3 halfAMillimeterOut = boundaryInTheWorld + wayOut * 0.0005f;
        Vector3 twoMillimetersOut = boundaryInTheWorld + wayOut * 0.002f;

        Assert.That(region.IsInRegion(halfAMillimeterOut), Is.True,
            $"half a millimeter out from {onTheBoundary} at a scale of {region.Scale}");
        Assert.That(region.IsInRegion(twoMillimetersOut), Is.False,
            $"two millimeters out from {onTheBoundary} at a scale of {region.Scale}");
    }

    /// <summary>Asserts that two vectors are equal within a small tolerance, component by component.</summary>
    internal static void AssertNearly(Vector3 actual, Vector3 expected)
    {
        const float tolerance = 0.001f;

        Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance));
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance));
    }

    /// <summary>Adds an L shaped footprint, a 10 by 10 square with its upper right quarter missing.</summary>
    internal static void AddLShapedFootprint(PolygonRegionBase region)
    {
        Vector2 lowerLeft = new(0f, 0f);
        Vector2 lowerRight = new(10f, 0f);
        Vector2 rightNotch = new(10f, 5f);
        Vector2 innerCorner = new(5f, 5f);
        Vector2 topNotch = new(5f, 10f);
        Vector2 upperLeft = new(0f, 10f);

        region.AddCorner(lowerLeft);
        region.AddCorner(lowerRight);
        region.AddCorner(rightNotch);
        region.AddCorner(innerCorner);
        region.AddCorner(topNotch);
        region.AddCorner(upperLeft);
    }
}
