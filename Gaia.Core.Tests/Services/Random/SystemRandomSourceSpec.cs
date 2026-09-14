using Gaia.Core.Services.Random;

namespace Gaia.Core.Tests.Services.Random;

/// <summary>Covers <see cref="SystemRandomSource" /> through its seed, the property that makes runs reproducible.</summary>
/// <remarks>The distribution itself belongs to System.Random and is not covered.</remarks>
[TestFixture]
public class SystemRandomSourceSpec
{
    [Test]
    public void ReproducesItsSequenceFromItsSeed()
    {
        SystemRandomSource source = new(7);
        SystemRandomSource replay = new(source.Seed);

        for (int draw = 0; draw < 5; draw++)
        {
            Assert.That(replay.NextFloat(), Is.EqualTo(source.NextFloat()), "the same seed produces the same sequence");
        }
    }

    [Test]
    public void TakesASeedOfItsOwnWhenGivenNone()
    {
        SystemRandomSource source = new();

        float firstDraw = source.NextFloat();

        SystemRandomSource replay = new(source.Seed);

        Assert.That(replay.NextFloat(), Is.EqualTo(firstDraw),
            "the self-chosen seed reproduces the run the same way a given one does");
    }
}
