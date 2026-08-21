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
}
