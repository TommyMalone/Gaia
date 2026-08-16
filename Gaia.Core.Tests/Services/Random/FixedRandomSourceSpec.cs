using Gaia.Core.Services.Random;

namespace Gaia.Core.Tests.Services.Random;

/// <summary>Covers <see cref="FixedRandomSource" />.</summary>
/// <remarks>
///     Specs normally configure the value in [0, 1), the way a real <see cref="IRandomSource.NextFloat" /> reports
///     it, though the tests here also use whole numbers to reach the integer overloads. What matters for the specs that
///     use this source is that every overload truncates and clamps into the bounds it was given, so that a reasoner or
///     weight function under test never reads outside a range it asked for.
/// </remarks>
[TestFixture]
public class FixedRandomSourceSpec
{
    [SetUp]
    public void BuildRandomSource()
    {
        _randomSource = new FixedRandomSource(ConfiguredValue);
    }

    private const float ConfiguredValue = 0.75f;

    private FixedRandomSource _randomSource;

    [Test]
    public void ReportsItsConfiguredValueAsAFloat()
    {
        Assert.That(_randomSource.NextFloat(), Is.EqualTo(ConfiguredValue));
    }

    [Test]
    public void ReportsItsConfiguredValueTruncatedAsAnInt()
    {
        FixedRandomSource wholeNumberSource = new(3f);

        Assert.Multiple(() =>
        {
            Assert.That(_randomSource.NextInt(), Is.EqualTo(0));
            Assert.That(wholeNumberSource.NextInt(), Is.EqualTo(3));
        });
    }

    [Test]
    public void ClampsIntoTheRangeItIsGiven()
    {
        FixedRandomSource wholeNumberSource = new(3f);

        Assert.Multiple(() =>
        {
            Assert.That(wholeNumberSource.NextInt(2), Is.EqualTo(1));
            Assert.That(wholeNumberSource.NextInt(10), Is.EqualTo(3));
            Assert.That(wholeNumberSource.NextInt(5, 10), Is.EqualTo(5));
            Assert.That(wholeNumberSource.NextInt(-10, -5), Is.EqualTo(-6));
        });
    }

    [Test]
    public void ReportsTheBoundWhenTheRangeIsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_randomSource.NextInt(0), Is.EqualTo(0));
            Assert.That(_randomSource.NextInt(7, 7), Is.EqualTo(7));
        });
    }

    [Test]
    public void RejectsARangeWhoseLowerBoundIsAboveItsUpperBound()
    {
        Assert.That(() => _randomSource.NextInt(5, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
