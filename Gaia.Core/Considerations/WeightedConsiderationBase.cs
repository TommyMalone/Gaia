using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     A consideration that queries one thing about the situation and hands the answer to a weight function, which
    ///     turns it into weight values.
    /// </summary>
    /// <remarks>
    ///     Splitting the two apart is what lets a single <see cref="DistanceConsideration" /> serve any number of
    ///     options. The class knows how to measure a distance, and the configured weight function decides what a given
    ///     distance means. The same distance can raise the addend on one option and veto another.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public abstract class WeightedConsiderationBase : ConsiderationBase
    {
        /// <summary>
        ///     The weight function this consideration hands its query result to. A consideration with none reports neutral
        ///     values.
        /// </summary>
        public WeightFunctionBase? WeightFunction { get; internal set; }

        /// <inheritdoc />
        public override void Calculate()
        {
            Weights = WeightFunction == null ? WeightValues.Neutral : CalculateWeights(WeightFunction);
        }

        /// <summary>
        ///     Queries the situation and converts the answer using the given weight function, by calling whichever of
        ///     <see cref="WeightFunctionBase.CalculateBool" />, <see cref="WeightFunctionBase.CalculateInt" />,
        ///     <see cref="WeightFunctionBase.CalculateFloat" /> or <see cref="WeightFunctionBase.CalculateString" /> matches the
        ///     type of the query.
        /// </summary>
        /// <param name="weightFunction">The configured weight function. Can never be null.</param>
        /// <returns>The weight values this consideration reports this cycle.</returns>
        protected abstract WeightValues CalculateWeights(WeightFunctionBase weightFunction);

        /// <inheritdoc />
        protected override void OnSelect()
        {
            WeightFunction?.Select();
        }

        /// <inheritdoc />
        protected override void OnDeselect()
        {
            WeightFunction?.Deselect();
        }
    }
}
