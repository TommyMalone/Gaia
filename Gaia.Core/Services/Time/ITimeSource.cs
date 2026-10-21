namespace Gaia.Core.Services.Time
{
    /// <summary>
    ///     Supplies the clock that time-dependent components read, such as an action that pauses for a number of seconds
    ///     or a consideration that reports how long it has been since an option last ran.
    /// </summary>
    /// <remarks>
    ///     Injected rather than accessed statically, so that a test can step time by hand and a paused or slowed game can
    ///     feed the AI its own clock rather than the wall clock.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.4.4</seealso>
    public interface ITimeSource
    {
        /// <summary>The time elapsed since the source started.</summary>
        /// <remarks>
        ///     Components record the reading at an event and subtract it from a later reading, so a brain the host updates
        ///     less often than once per tick still measures its pauses and cooldowns in full. Every brain reads the same clock,
        ///     which the host advances once per tick, so a reading of the time since the last advance would cover one tick rather
        ///     than the time since a given brain's last cycle.
        /// </remarks>
        Seconds CurrentTime { get; }
    }
}
