using Gaia.Core.Strings;

namespace Gaia.Core.Services.Log
{
    /// <summary>A sink that discards everything, for a host that wants no logging.</summary>
    public sealed class NullLogSink : ILogSink
    {
        /// <summary>Always false, so that a caller can skip building messages nobody will see.</summary>
        public bool IsEnabled(LogSeverity severity)
        {
            return false;
        }

        /// <summary>Discards the entry.</summary>
        public void Write(LogSeverity severity, AiString channel, string message)
        {
        }
    }
}
