using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A sink that records every entry it is given, so that a spec can assert on what was logged.</summary>
internal sealed class RecordingLogSink : ILogSink
{
    private readonly List<string> _entries = new();

    /// <summary>The recorded entries, each one line of severity, channel, and message.</summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <summary>Always true, so that specs exercise the write path.</summary>
    public bool IsEnabled(LogSeverity severity)
    {
        return true;
    }

    /// <summary>Records the entry.</summary>
    public void Write(LogSeverity severity, AiString channel, string message)
    {
        _entries.Add($"{severity} {channel.Text} {message}");
    }
}
