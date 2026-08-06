using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A weight function that reports whatever it was passed and counts the lifecycle calls it receives. For specs
///     that care about what reached the weight function rather than what it computed.
/// </summary>
internal sealed class RecordingWeightFunction : WeightFunctionBase
{
    /// <summary>The values every conversion reports.</summary>
    internal WeightValues Result { get; set; } = WeightValues.Neutral;

    /// <summary>The value passed to the most recent CalcFloat.</summary>
    internal float LastValue { get; private set; }

    /// <summary>How many times Select has been called.</summary>
    internal int SelectCount { get; private set; }

    /// <summary>How many times Deselect has been called.</summary>
    internal int DeselectCount { get; private set; }

    /// <inheritdoc />
    public override bool Init(AiCreationData creationData)
    {
        // Configurations cannot presently be loaded successfully.
        return false;
    }

    /// <summary>Remembers the value as LastFloat, counts the call and reports Result.</summary>
    public override WeightValues CalcFloat(float value)
    {
        LastValue = value;

        return Result;
    }

    /// <summary>Counts the call in SelectCount.</summary>
    public override void Select()
    {
        SelectCount++;
    }

    /// <summary>Counts the call in DeselectCount.</summary>
    public override void Deselect()
    {
        DeselectCount++;
    }
}
