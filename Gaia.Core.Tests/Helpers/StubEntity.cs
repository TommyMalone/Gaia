using Gaia.Core.Entities;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A concrete entity for specs that need something to point at.</summary>
internal sealed class StubEntity : EntityBase
{
    /// <summary>Creates an entity with the given name.</summary>
    internal StubEntity(string name)
    {
        Name = name;
    }
}
