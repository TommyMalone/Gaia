using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.Tests.Helpers;

/// <summary>Builds the creation data a loading test hands to an Init.</summary>
/// <remarks>
///     The source name is fixed at guard.xml, so every loading spec asserts its errors under one prefix whatever its
///     snippet configures.
/// </remarks>
internal static class LoadingSpecHelpers
{
    /// <summary>Parses the snippet and wraps its root node in creation data reporting to the sink.</summary>
    internal static AiCreationData ParseForLoad(string snippet, RecordingLogSink logSink)
    {
        XmlSpecificationReader reader = new(logSink);
        reader.TryRead(snippet, "guard.xml", out SpecificationNode? root);

        return new AiCreationData(root!, new FactorySet()) { LogSink = logSink };
    }
}
