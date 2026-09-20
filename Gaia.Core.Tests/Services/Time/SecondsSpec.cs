using Gaia.Core.Services.Time;

namespace Gaia.Core.Tests.Services.Time;

/// <summary>Covers <see cref="Seconds" />.</summary>
[TestFixture]
public class SecondsSpec
{
    [Test]
    public void ReadsBackTheNumberItWasMadeFrom()
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
        // An elapsed time exactly equal to a PauseAction's Duration is common when a host advances its clock in fixed
        // steps, so the boundary has to count.
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

    [Test]
    public void KeepsAdvancingWhereAFloatWouldStop()
    {
        // Near 145 hours of session, 2^19 seconds, a tick's delta falls below half a float unit of precision, so
        // a float-backed clock rounds the addition away and stands still. The double keeps counting.
        Seconds longSession = new(524288.0);
        Seconds frameTime = new(1.0 / 60.0);

        float floatSession = (float)longSession.Value;
        float floatAdvanced = floatSession + (float)frameTime.Value;

        Seconds advanced = longSession + frameTime;

        Assert.Multiple(() =>
        {
            Assert.That(floatAdvanced, Is.EqualTo(floatSession),
                "a float clock has stopped here, which is the horizon this moves");
            Assert.That(advanced.Value, Is.GreaterThan(longSession.Value));
        });
    }
}
