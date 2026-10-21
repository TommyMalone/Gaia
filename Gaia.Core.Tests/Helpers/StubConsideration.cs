using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A consideration whose result is set by hand rather than queried from a world.</summary>
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
    protected override bool OnInit(AiCreationData creationData)
    {
        // Built in code with its values set by hand, so a load is a spec mistake and is reported as one.
        return false;
    }

    /// <summary>Does nothing, since Weights is set by hand rather than calculated.</summary>
    public override void Calculate()
    {
    }
}
