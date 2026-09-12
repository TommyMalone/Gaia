using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.HostSpecs;

/// <summary>A sink built against the public ILogSink alone, keeping entries in a list the way a game console would.</summary>
internal sealed class ListLogSink : ILogSink
{
    private readonly List<string> _entries = new();

    /// <summary>Every recorded entry, one line of severity, channel, and message.</summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <summary>The recorded entries at <see cref="LogSeverity.Error" />.</summary>
    public List<string> Errors
    {
        get
        {
            List<string> errors = new();

            foreach (string entry in _entries)
            {
                if (entry.StartsWith(nameof(LogSeverity.Error)))
                {
                    errors.Add(entry);
                }
            }

            return errors;
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(LogSeverity severity)
    {
        return true;
    }

    /// <inheritdoc />
    public void Write(LogSeverity severity, AiString channel, string message)
    {
        _entries.Add($"{severity} {channel.Text} {message}");
    }
}
