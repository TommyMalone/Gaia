using Gaia.Core.Strings;

namespace Gaia.Core.Services.Log
{
    /// <summary>The methods call sites use to write an entry, one per severity.</summary>
    /// <remarks>
    ///     These are extension methods rather than interface members, so that a call site reads as LogWarning while a
    ///     sink implements a single Write.
    /// </remarks>
    /// <seealso>
    ///     Microsoft.Extensions.Logging.LoggerExtensions, which names the levels the same way over a single Log,
    ///     described at https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loggerextensions
    /// </seealso>
    public static class LogSinkExtensions
    {
        /// <summary>Writes an entry at <see cref="LogSeverity.Message" />.</summary>
        public static void LogMessage(this ILogSink logSink, AiString channel, string message)
        {
            logSink.Write(LogSeverity.Message, channel, message);
        }

        /// <summary>Writes an entry at <see cref="LogSeverity.Warning" />.</summary>
        public static void LogWarning(this ILogSink logSink, AiString channel, string message)
        {
            logSink.Write(LogSeverity.Warning, channel, message);
        }

        /// <summary>Writes an entry at <see cref="LogSeverity.Error" />.</summary>
        public static void LogError(this ILogSink logSink, AiString channel, string message)
        {
            logSink.Write(LogSeverity.Error, channel, message);
        }
    }
}
