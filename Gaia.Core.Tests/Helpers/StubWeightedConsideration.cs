using Gaia.Core.Considerations;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A weighted consideration whose query result is set by hand, standing in for one that measures something about
///     a world.
/// </summary>
/// <remarks>
///     It records how many times it was asked to convert, so that a spec can tell whether
///     <see cref="WeightedConsiderationBase" /> reached CalculateWeights at all.
/// </remarks>
internal sealed class StubWeightedConsideration : WeightedConsiderationBase
{
    /// <summary>The value handed to the weight function in place of a real measurement.</summary>
    internal float QueryResult { get; set; }

    /// <summary>How many times CalculateWeights has run.</summary>
    internal int CalculateWeightsCount { get; private set; }

    /// <inheritdoc />
    public override bool Init(AiCreationData creationData)
    {
        // Configurations cannot presently be loaded successfully.
        return false;
    }

    /// <summary>Hands QueryResult to the weight function as a float, counting the call.</summary>
    protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction)
    {
        CalculateWeightsCount++;

        return weightFunction.CalcFloat(QueryResult);
    }
}
