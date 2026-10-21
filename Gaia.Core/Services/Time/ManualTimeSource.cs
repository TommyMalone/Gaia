namespace Gaia.Core.Services.Time
{
    /// <summary>
    ///     A clock that moves only when Advance is called. The host calls Advance once per tick with whatever elapsed
    ///     time the game reports, which keeps the AI on the game's clock rather than the wall clock and makes it stop when the
    ///     game is paused. A test advances it by hand to put a cooldown or a pause where it needs one.
    /// </summary>
    public sealed class ManualTimeSource : ITimeSource
    {
        /// <inheritdoc />
        public Seconds CurrentTime { get; private set; }

        /// <summary>Moves the clock forward. The host calls it once per tick, before anything reads the time.</summary>
        /// <param name="delta">The time elapsed since the previous tick.</param>
        public void Advance(Seconds delta)
        {
            CurrentTime += delta;
        }
    }
}
