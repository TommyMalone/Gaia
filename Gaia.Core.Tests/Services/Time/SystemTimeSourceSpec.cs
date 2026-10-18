using Gaia.Core.Services.Time;

namespace Gaia.Core.Tests.Services.Time;

/// <summary>Covers <see cref="SystemTimeSource" />.</summary>
[TestFixture]
public class SystemTimeSourceSpec
{
    [Test]
    public void AdvancesWhenTicked()
    {
        // The source's stopwatch starts when the source is made, and its time stands at zero until the first tick, so
        // a tick after a short wait reads a later time than the one before it.
        SystemTimeSource timeSource = new();
        Seconds beforeTheTick = timeSource.CurrentTime;

        Thread.Sleep(20);
        timeSource.Tick();

        Assert.That(timeSource.CurrentTime > beforeTheTick, Is.True);
    }
}
