using Gaia.Core.Regions;
using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Regions;

/// <summary>The asserts and builders the region specs share.</summary>
internal static class RegionSpecHelpers
{
    /// <summary>How many random positions the sampling asserts draw.</summary>
    internal const int SampleCount = 50;

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
