using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.Tests.Helpers;

/// <summary>Builds the creation data a loading test hands to an Init.</summary>
/// <remarks>
///     The snippet is parsed through <see cref="XmlSpecificationReader" /> under the source name guard.xml, and the
///     creation data reports to the given sink, so a spec asserts loading errors the same way it asserts reader errors.
/// </remarks>
internal static class LoadingSpecHelpers
{
    /// <summary>Parses the snippet and wraps its root node in creation data reporting to the sink.</summary>
    internal static AiCreationData ParseForLoad(string snippet, RecordingLogSink logSink)
    {
        XmlSpecificationReader reader = new() { LogSink = logSink };
        reader.TryRead(snippet, "guard.xml", out SpecificationNode? root);

        return new AiCreationData(root!, new FactorySet()) { LogSink = logSink };
    }
}
