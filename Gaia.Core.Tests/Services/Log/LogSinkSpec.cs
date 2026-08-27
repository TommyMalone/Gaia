using System.Diagnostics;
using Gaia.Core.Services.Log;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Services.Log;

/// <summary>Covers <see cref="ILogSink" />, <see cref="TraceLogSink" />, and <see cref="NullLogSink" />.</summary>
/// <remarks>
///     The per-severity methods are exercised against a <see cref="RecordingLogSink" />, which is also how any other
///     spec would observe logging. <see cref="TraceLogSink" /> is observed through a listener attached to
///     <see cref="Trace" /> for the duration of a test, and <see cref="NullLogSink" /> is shown to discard.
/// </remarks>
[TestFixture]
public class LogSinkSpec
{
    [SetUp]
    public void BuildSinks()
    {
        _recordingSink = new RecordingLogSink();
        _capturedLines = new List<string>();
        _traceListener = new CapturingTraceListener(_capturedLines);
        Trace.Listeners.Add(_traceListener);
    }

    [TearDown]
    public void DetachTheListener()
    {
        Trace.Listeners.Remove(_traceListener);
        _traceListener.Dispose();
    }

    private RecordingLogSink _recordingSink;
    private List<string> _capturedLines;
    private CapturingTraceListener _traceListener;

    [Test]
    public void WritesTheSeverityThatMatchesTheMethodCalled()
    {
        _recordingSink.LogMessage("Guard", "patrolling");
        _recordingSink.LogWarning("Guard", "no waypoints");
        _recordingSink.LogError("Guard", "no brain");

        Assert.That(_recordingSink.Entries, Is.EqualTo(new[]
        {
            "Message Guard patrolling",
            "Warning Guard no waypoints",
            "Error Guard no brain",
        }));
    }

    [Test]
    public void IsEnabledForEverySeverityAsATraceSink()
    {
        TraceLogSink sink = new();

        Assert.Multiple(() =>
        {
            Assert.That(sink.IsEnabled(LogSeverity.Message), Is.True);
            Assert.That(sink.IsEnabled(LogSeverity.Warning), Is.True);
            Assert.That(sink.IsEnabled(LogSeverity.Error), Is.True);
        });
    }

    [Test]
    public void WritesOneLineThroughTraceAsATraceSink()
    {
        TraceLogSink sink = new();

        sink.LogWarning("Guard", "no waypoints");

        Assert.That(_capturedLines, Is.EqualTo(new[] { "Warning Guard no waypoints" }));
    }

    [Test]
    public void IsEnabledForNothingAsANullSink()
    {
        NullLogSink sink = new();

        Assert.Multiple(() =>
        {
            Assert.That(sink.IsEnabled(LogSeverity.Message), Is.False);
            Assert.That(sink.IsEnabled(LogSeverity.Warning), Is.False);
            Assert.That(sink.IsEnabled(LogSeverity.Error), Is.False);
        });
    }

    [Test]
    public void DiscardsWhatItIsGivenAsANullSink()
    {
        NullLogSink sink = new();

        sink.LogError("Guard", "no brain");

        Assert.That(_capturedLines, Is.Empty);
    }

    /// <summary>A listener that appends each full line <see cref="Trace" /> is given to a list the spec owns.</summary>
    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly List<string> _lines;

        internal CapturingTraceListener(List<string> lines)
        {
            _lines = lines;
        }

        /// <summary>Ignores partial writes, which the sink never makes.</summary>
        public override void Write(string? message)
        {
        }

        /// <summary>Records the line.</summary>
        public override void WriteLine(string? message)
        {
            if (message != null)
            {
                _lines.Add(message);
            }
        }
    }
}
