using System.Diagnostics;
using Gaia.Core.Strings;

namespace Gaia.Core.Services.Log
{
    /// <summary>The default sink, which writes every entry through <see cref="Trace" />.</summary>
    public sealed class TraceLogSink : ILogSink
    {
        /// <summary>Always true, since the listeners decide for themselves what to keep.</summary>
        public bool IsEnabled(LogSeverity severity)
        {
            return true;
        }

        /// <summary>Writes the entry as one line, the severity and the channel in front of the message.</summary>
        public void Write(LogSeverity severity, AiString channel, string message)
        {
            Trace.WriteLine($"{severity} {channel.Text} {message}");
        }
    }
}
