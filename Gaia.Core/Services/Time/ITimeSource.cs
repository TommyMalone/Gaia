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
        /// <remarks>
        ///     The one clock reading components take. Components store the absolute time at their events and compare later,
        ///     which is what lets a brain run its cycles less often than the host ticks without anything drifting. A per-cycle
        ///     delta would be the host tick's step, not any one brain's.
        /// </remarks>
        Seconds CurrentTime { get; }
    }
}
