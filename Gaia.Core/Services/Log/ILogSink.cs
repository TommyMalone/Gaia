using Gaia.Core.Strings;

namespace Gaia.Core.Services.Log
{
    /// <summary>
    ///     Where the AI's log entries go. Supplied by the host, so the same component code reports through whatever
    ///     console or file the game already has.
    /// </summary>
    /// <remarks>See <see cref="LogSinkExtensions" /> for the methods call sites are expected to use.</remarks>
    /// <seealso>"Modular AI", Section 8.4.4.3</seealso>
    /// <seealso>
    ///     Logging guidance for .NET library authors, described at
    ///     https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/library-guidance
    /// </seealso>
    public interface ILogSink
    {
        /// <summary>Reports whether entries of the given severity go anywhere.</summary>
        /// <remarks>
        ///     A caller about to build an expensive message checks this first and skips the work when the sink would discard
        ///     it.
        /// </remarks>
        bool IsEnabled(LogSeverity severity);

        /// <summary>Writes one entry.</summary>
        /// <param name="severity">How important the entry is.</param>
        /// <param name="channel">
        ///     What the entry is about, such as a character's name, so that output can be filtered to one
        ///     subject.
        /// </param>
        /// <param name="message">What happened.</param>
        void Write(LogSeverity severity, AiString channel, string message);
    }
}
