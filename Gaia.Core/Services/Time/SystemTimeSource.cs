using System.Diagnostics;

namespace Gaia.Core.Services.Time
{
    /// <summary>A clock backed by real elapsed time, for a host that has no clock of its own to feed in.</summary>
    public sealed class SystemTimeSource : ITimeSource
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <inheritdoc />
        public Seconds CurrentTime { get; private set; }

        /// <inheritdoc />
        public Seconds DeltaTime { get; private set; }

        /// <summary>
        ///     Sample the elapsed time. Call once per cycle, before anything reads the clock, so that everything deciding on
        ///     the same cycle agrees about what time it is.
        /// </summary>
        public void Tick()
        {
            Seconds elapsedTime = new((float)_stopwatch.Elapsed.TotalSeconds);

            DeltaTime = elapsedTime - CurrentTime;
            CurrentTime = elapsedTime;
        }
    }
}