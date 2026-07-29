using Gaia.Core.Considerations;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A consideration whose result is set by hand rather than queried from a world. Stands in for
///     EntityExistsConsideration, ExecutionHistoryConsideration and the rest. A
///     multiplier of 0 vetoes the option regardless of what the other considerations contribute.
/// </summary>
internal sealed class StubConsideration : ConsiderationBase
{
    /// <summary>Creates a consideration that reports the given weights until Set changes them.</summary>
    internal StubConsideration(WeightValues weights)
    {
        Weights = weights;
    }

    /// <summary>Reports the given Weights from here on.</summary>
    internal void Set(WeightValues weights)
    {
        Weights = weights;
    }

    /// <summary>Contributes nothing and vetoes nothing, leaving the option's other considerations to decide.</summary>
    internal void Pass()
    {
        Set(WeightValues.Neutral);
    }

    /// <summary>Vetoes the option outright with a multiplier of 0.</summary>
    internal void Veto()
    {
        Set(WeightValues.Vetoed);
    }

    /// <inheritdoc />
    public override bool Init(AiCreationData creationData)
    {
        // Configurations cannot presently be loaded successfully.
        return false;
    }

    /// <summary>Does nothing, since Weights is set by hand rather than calculated.</summary>
    public override void Calculate()
    {
    }
}