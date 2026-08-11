using Gaia.Core.Strings;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A weight function that remembers what it was passed and counts the calls it receives, reporting the configured
///     Result, for specs that care about what reached the weight function rather than what it computed.
/// </summary>
internal sealed class RecordingWeightFunction : WeightFunctionBase
{
    /// <summary>The values every conversion reports.</summary>
    internal WeightValues Result { get; set; } = WeightValues.Neutral;

    /// <summary>The value passed to the most recent CalcFloat.</summary>
    internal float LastFloat { get; private set; }

    /// <summary>The value passed to the most recent CalcBool.</summary>
    internal bool LastBool { get; private set; }

    /// <summary>The value passed to the most recent CalcInt.</summary>
    internal int LastInt { get; private set; }

    /// <summary>The value passed to the most recent CalcString.</summary>
    internal AiString LastString { get; private set; }

    /// <summary>How many times a conversion of any kind has been asked for.</summary>
    internal int CalculationCount { get; private set; }

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

    /// <summary>Remembers the value as LastBool, counts the call and reports Result.</summary>
    public override WeightValues CalcBool(bool value)
    {
        LastBool = value;
        CalculationCount++;

        return Result;
    }

    /// <summary>Overridden rather than left to the base, so that a spec can tell an int apart from a float.</summary>
    public override WeightValues CalcInt(int value)
    {
        LastInt = value;
        CalculationCount++;

        return Result;
    }

    /// <summary>Remembers the value as LastFloat, counts the call and reports Result.</summary>
    public override WeightValues CalcFloat(float value)
    {
        LastFloat = value;
        CalculationCount++;

        return Result;
    }

    /// <summary>Remembers the value as LastString, counts the call and reports Result.</summary>
    public override WeightValues CalcString(AiString value)
    {
        LastString = value;
        CalculationCount++;

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
