using Gaia.Core.Services.Time;

namespace Gaia.Core.Tests.Services.Time;

/// <summary>Covers <see cref="Seconds" />.</summary>
[TestFixture]
public class SecondsSpec
{
    [Test]
    public void ReadsBackTheFloatItWasMadeFrom()
    {
        Seconds time = new(2.5f);

        Assert.That(time.Value, Is.EqualTo(2.5f));
    }

    [Test]
    public void AddsTwoTimesTogether()
    {
        Seconds runTime = new(3f);
        Seconds coolTime = new(2f);
        Seconds totalTime = new(5f);

        Assert.That(runTime + coolTime, Is.EqualTo(totalTime));
    }

    [Test]
    public void SubtractsOneTimeFromAnother()
    {
        Seconds laterTime = new(5f);
        Seconds earlierTime = new(2f);
        Seconds betweenTime = new(3f);

        Assert.That(laterTime - earlierTime, Is.EqualTo(betweenTime));
    }

    [Test]
    public void ComparesTimesByLength()
    {
        Seconds shorterTime = new(1f);
        Seconds longerTime = new(2f);

        Assert.Multiple(() =>
        {
            Assert.That(shorterTime < longerTime, Is.True);
            Assert.That(longerTime > shorterTime, Is.True);
            Assert.That(shorterTime > longerTime, Is.False);
        });
    }

    [Test]
    public void IncludesTheBoundaryInItsOrEqualComparisons()
    {
        // A duration that lands exactly on a cycle boundary is common, so the boundary has to count.
        Seconds time = new(2f);
        Seconds sameTime = new(2f);

        Assert.Multiple(() =>
        {
            Assert.That(time <= sameTime, Is.True);
            Assert.That(time >= sameTime, Is.True);
        });
    }

    [Test]
    public void EqualsAnotherTimeOfTheSameLength()
    {
        Seconds time = new(2f);
        Seconds sameTime = new(2f);
        Seconds differentTime = new(3f);

        Assert.Multiple(() =>
        {
            Assert.That(time.Equals(sameTime), Is.True);
            Assert.That(time == sameTime, Is.True);
            Assert.That(time != differentTime, Is.True);
            Assert.That(time.Equals(differentTime), Is.False);
        });
    }

    [Test]
    public void EqualsTheSameLengthWhenBoxed()
    {
        Seconds time = new(2f);
        object boxedSameTime = new Seconds(2f);

        Assert.That(time.Equals(boxedSameTime), Is.True);
    }

    [Test]
    public void SharesItsHashWithAnEqualTime()
    {
        Seconds time = new(2f);
        Seconds sameTime = new(2f);

        Assert.That(time.GetHashCode(), Is.EqualTo(sameTime.GetHashCode()));
    }

    [Test]
    public void PrintsItsSecondsWithTheUnit()
    {
        Seconds time = new(2.5f);

        Assert.That(time.ToString(), Is.EqualTo("2.5s"));
    }

    [Test]
    public void StartsFromNoTimeAtAll()
    {
        Assert.That(Seconds.Zero.Value, Is.EqualTo(0f));
    }
}