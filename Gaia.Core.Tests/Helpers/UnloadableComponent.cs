using Gaia.Core.Factories;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A component whose Init always fails, for specs that cover a factory's failure path.</summary>
internal sealed class UnloadableComponent : IAiComponent
{
    /// <summary>Reports failure without loading anything.</summary>
    public bool Init(AiCreationData creationData)
    {
        return false;
    }
}
