using Gaia.Core.DataStores;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A concrete data store that records type mismatches for the spec to read.</summary>
internal sealed class StubDataStore : DataStoreBase
{
    private readonly List<string> _typeMismatches = new();

    /// <summary>Every mismatch reported so far, as "Name stored requested".</summary>
    internal IReadOnlyList<string> TypeMismatches => _typeMismatches;

    /// <summary>Records the mismatch for TypeMismatches to report.</summary>
    protected override void OnTypeMismatch(
        AiString name,
        DataStoreValueType storedValueType,
        DataStoreValueType requestedValueType)
    {
        _typeMismatches.Add($"{name} {storedValueType} {requestedValueType}");
    }
}
