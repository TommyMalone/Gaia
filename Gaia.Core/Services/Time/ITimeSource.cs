namespace Gaia.Core.Services.Time
{
    /// <summary>
    ///     Supplies the clock that time-dependent components read, such as an action that pauses for a number of seconds
    ///     or a consideration that reports how long it has been since an option last ran. Injected rather than accessed
    ///     statically, so that tests can step time by hand and so that a paused or slowed game can feed the AI its own clock
    ///     rather than the wall clock.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.4.4.4</seealso>
    public interface ITimeSource
    {
        /// <summary>The time elapsed since the source started.</summary>
        Seconds CurrentTime { get; }

        /// <summary>The time elapsed between the previous cycle and this one.</summary>
        Seconds DeltaTime { get; }
    }
}