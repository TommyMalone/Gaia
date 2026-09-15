using Gaia.Core.Services.Log;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Strings;

/// <summary>Covers <see cref="AiStringTable" /> reporting a hash collision, the failure that aliases two names.</summary>
/// <remarks>
///     The table is process wide, so the spec swaps its sink in and restores the standing one afterward. The first of
///     the two colliding strings stays in the table for the rest of the run, and no other string the suite makes shares
///     its hash.
/// </remarks>
[TestFixture]
public class AiStringTableSpec
{
    [Test]
    public void ReportsACollisionThroughItsSinkInEveryBuild()
    {
        // AiStringTable.ComputeHash is djb2, which is linear over its characters, so a two-character collision can be
        // written down directly. With lower-cased characters a1, a2 the hash is 5381 * 1089 + 33 * a1 + a2, and 33 * 98
        // + 97 equals 33 * 97 + 130, so "ba" collides with "a" followed by the character 130.
        RecordingLogSink logSink = new();
        ILogSink standingSink = AiStringTable.LogSink;
        AiStringTable.LogSink = logSink;

        try
        {
            AiString first = "ba";
            AiString collider = "a\u0082";

            Assert.Multiple(() =>
            {
                Assert.That(first == collider, Is.True, "the two texts share a hash");
                Assert.That(logSink.Entries, Has.Some.Contains("hash to the same value"));
            });
        }
        finally
        {
            AiStringTable.LogSink = standingSink;
        }
    }
}
