using System.Diagnostics;

namespace Gaia.Core.Services.Time
{
    /// <summary>
    ///     A clock that reads elapsed wall-clock time from a Stopwatch started when it is constructed, for a host that
    ///     has no clock of its own to feed in.
    /// </summary>
    public sealed class SystemTimeSource : ITimeSource
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <inheritdoc />
        public Seconds CurrentTime { get; private set; }

        /// <summary>
        ///     Samples the elapsed time. The host calls it once per tick, before any brain reads the clock, so that
        ///     everything deciding on that tick agrees about what time it is.
        /// </summary>
        public void Tick()
        {
            CurrentTime = new Seconds(_stopwatch.Elapsed.TotalSeconds);
        }
    }
}
