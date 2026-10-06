using Gaia.Core.Factories;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A component whose Init succeeds, keeping the creation data it was given so a spec can assert on it.</summary>
internal sealed class LoadableComponent : IAiComponent
{
    /// <summary>The creation data the last Init received, or null while none has run.</summary>
    public AiCreationData? ReceivedCreationData { get; private set; }

    /// <summary>How many times Init has run, so a spec can tell a refusal made before Init from one made after it.</summary>
    public int InitCount { get; private set; }

    /// <summary>Keeps the creation data, counts the call and reports success.</summary>
    public bool Init(AiCreationData creationData)
    {
        ReceivedCreationData = creationData;
        InitCount++;
        return true;
    }
}
