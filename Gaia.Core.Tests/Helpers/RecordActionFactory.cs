using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     Builds <see cref="RecordAction" /> instances that share one log, one sink and one channel, so a spec that
///     builds many actions for one character passes the three once rather than on every action.
/// </summary>
internal sealed class RecordActionFactory
{
    private readonly AiString _channel;
    private readonly List<string> _log;
    private readonly ILogSink _logSink;

    /// <summary>Creates a factory whose actions all record into the given list and report through the given sink.</summary>
    /// <param name="log">The shared list every action's lifecycle calls are appended to.</param>
    /// <param name="logSink">The sink every action reports through.</param>
    /// <param name="channel">The character name every action's entries are filed under.</param>
    internal RecordActionFactory(List<string> log, ILogSink logSink, AiString channel)
    {
        _log = log;
        _logSink = logSink;
        _channel = channel;
    }

    /// <summary>Builds an action wired to the shared list, sink, and channel.</summary>
    /// <param name="name">The name the action records and logs under.</param>
    /// <param name="isDone">
    ///     The initial IsDone, defaulting to true the way <see cref="RecordAction" /> does. Pass false for an
    ///     action that keeps its option running until SetDone is called.
    /// </param>
    internal RecordAction Build(string name, bool isDone = true)
    {
        return new RecordAction(name, _log, isDone)
        {
            LogSink = _logSink,
            LogChannel = _channel,
        };
    }
}
