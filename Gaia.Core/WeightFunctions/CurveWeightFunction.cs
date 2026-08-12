using Gaia.Core.Curves;
using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Runs the input through a response curve.</summary>
    /// <remarks>
    ///     <para>
    ///         AddendCurve and MultiplierCurve each map the input on their own, so one configuration can raise an option's
    ///         desire as a value climbs while another vetoes it outside a band. A channel with no curve reports its identity,
    ///         which is 0 for the addend and 1 for the multiplier, and so has no effect on the option's weight.
    ///     </para>
    ///     <para>
    ///         Rank is reported as configured rather than curved, because it names a tier rather than a quantity and a value
    ///         part way between two tiers means nothing.
    ///     </para>
    /// </remarks>
    public class CurveWeightFunction : WeightFunctionBase
    {
        /// <summary>The curve mapping the input to an addend, or null to contribute no addend.</summary>
        public Curve? AddendCurve { get; internal set; }

        /// <summary>The curve mapping the input to a multiplier, or null to leave the multiplier alone.</summary>
        public Curve? MultiplierCurve { get; internal set; }

        /// <summary>The rank to report, whatever the input.</summary>
        public float Rank { get; internal set; } = float.MinValue;

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override WeightValues CalculateFloat(float value)
        {
            float addend = AddendCurve?.Evaluate(value) ?? 0f;
            float multiplier = MultiplierCurve?.Evaluate(value) ?? 1f;

            return new WeightValues(addend, multiplier, Rank);
        }
    }
}
