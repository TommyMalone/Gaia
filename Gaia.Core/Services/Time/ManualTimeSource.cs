namespace Gaia.Core.Services.Time
{
    /// <summary>
    ///     A clock that only moves when it is told to. The host calls Advance once per cycle with whatever elapsed time
    ///     the game reports, which keeps the AI on the game's clock rather than the wall clock and makes it stop when the game
    ///     is paused. A spec advances it by hand to put a cooldown or a pause wherever the test needs it.
    /// </summary>
    public sealed class ManualTimeSource : ITimeSource
    {
        /// <inheritdoc />
        public Seconds CurrentTime { get; private set; }

        /// <summary>Move the clock forward. Call once per cycle, before anything reads the time.</summary>
        /// <param name="delta">The time elapsed since the previous cycle.</param>
        public void Advance(Seconds delta)
        {
            CurrentTime += delta;
        }
    }
}
