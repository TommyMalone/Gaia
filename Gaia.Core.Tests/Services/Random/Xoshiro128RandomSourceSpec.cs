using Gaia.Core.Services.Random;

namespace Gaia.Core.Tests.Services.Random;

/// <summary>Covers <see cref="Xoshiro128RandomSource" />, the seed that reproduces runs and the sequence the seed pins.</summary>
/// <remarks>
///     The pinned-draw tests hold the generator to exact values, so a change to the algorithm, the seeding or the
///     draw mechanics fails here before it silently invalidates every recorded run. The expected floats are written as
///     draws over 2^24, the form NextFloat produces them in.
/// </remarks>
[TestFixture]
public class Xoshiro128RandomSourceSpec
{
    [SetUp]
    public void BuildRandomSource()
    {
        _source = new Xoshiro128RandomSource(Seed);
    }

    private const int Seed = 7;
    private const float Precision = 1 << 24;

    private Xoshiro128RandomSource _source;

    [Test]
    public void ReproducesItsSequenceFromItsSeed()
    {
        Xoshiro128RandomSource replay = new(_source.Seed);

        for (int draw = 0; draw < 5; draw++)
        {
            Assert.That(replay.NextFloat(), Is.EqualTo(_source.NextFloat()),
                "the same seed produces the same sequence");
        }
    }

    [Test]
    public void TakesASeedOfItsOwnWhenGivenNone()
    {
        Xoshiro128RandomSource source = new();

        float firstDraw = source.NextFloat();

        Xoshiro128RandomSource replay = new(source.Seed);

        Assert.That(replay.NextFloat(), Is.EqualTo(firstDraw),
            "the self-chosen seed reproduces the run the same way a given one does");
    }

    [Test]
    public void ProducesTheSameSequenceOnEveryRuntime()
    {
        // These are the first three draws of seed 7, computed from the published xoshiro128** and splitmix64
        // definitions outside this codebase. They hold on any runtime because this codebase implements the generator.
        Assert.Multiple(() =>
        {
            Assert.That(_source.NextFloat(), Is.EqualTo(7035534f / Precision));
            Assert.That(_source.NextFloat(), Is.EqualTo(6071585f / Precision));
            Assert.That(_source.NextFloat(), Is.EqualTo(11690628f / Precision));
        });
    }

    [Test]
    public void BoundsItsDrawsExactly()
    {
        Assert.Multiple(() =>
        {
            // The first three 32-bit values of seed 7 modulo 100, pinned the same way the floats are.
            Assert.That(_source.NextInt(100), Is.EqualTo(69));
            Assert.That(_source.NextInt(100), Is.EqualTo(24));
            Assert.That(_source.NextInt(100), Is.EqualTo(42));
            Assert.That(_source.NextInt(0), Is.EqualTo(0), "a bound of 0 returns 0");
            Assert.That(() => _source.NextInt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void StaysInsideARangeThatSpansZero()
    {
        for (int draw = 0; draw < 100; draw++)
        {
            int value = _source.NextInt(-5, 5);

            Assert.That(value, Is.InRange(-5, 4));
        }
    }

    [Test]
    public void SeedsDefaultSourcesApartHoweverFastTheyAreMade()
    {
        // The default constructor combines the Environment.TickCount reading with a count of default constructions. Two
        // constructions at the same reading would share a seed without the count, and correlated sources are the
        // failure a default should never quietly hand out.
        Xoshiro128RandomSource firstSource = new();
        Xoshiro128RandomSource secondSource = new();

        Assert.That(firstSource.Seed, Is.Not.EqualTo(secondSource.Seed));
    }
}
