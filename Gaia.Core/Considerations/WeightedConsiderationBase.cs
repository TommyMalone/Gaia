using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
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
        internal static readonly AiString WeightFunctionElement = "WeightFunction";

        /// <summary>
        ///     The weight function this consideration hands its query result to. A consideration with none reports neutral
        ///     values.
        /// </summary>
        /// <remarks>
        ///     Settable by a derived type as well as by loading, so a game's own consideration built outside the assembly can
        ///     hold a weight function of the game's own.
        /// </remarks>
        public WeightFunctionBase? WeightFunction { get; protected internal set; }

        /// <summary>The kind of value this consideration hands its weight function, such as "a float".</summary>
        /// <remarks>
        ///     A noun phrase carrying its own article, allowing a variable nothing has written to be reported as "never been
        ///     written as a float".
        /// </remarks>
        protected abstract string QueryPhrase { get; }

        /// <summary>Builds a WeightFunction child element into <see cref="WeightFunction" />.</summary>
        /// <remarks>
        ///     The function's kind is checked here, at load, because a function that cannot convert what the consideration
        ///     measures would otherwise load clean and throw on the decision path the first time the option is evaluated.
        /// </remarks>
        protected internal bool TryLoadWeightFunction(SpecificationNode child, AiCreationData creationData)
        {
            if (!creationData.Factories.WeightFunctions.TryCreate(creationData.ForChild(child),
                    out WeightFunctionBase? weightFunction))
            {
                return false;
            }

            if (!CanConvert(weightFunction!))
            {
                creationData.ReportError(
                    $"Line {child.LineNumber}, the WeightFunction element's type {child.Type.Text} does not convert {QueryPhrase}");
                return false;
            }

            WeightFunction = weightFunction;
            return true;
        }

        /// <summary>True when the given function converts the kind of value this consideration hands it.</summary>
        protected abstract bool CanConvert(WeightFunctionBase weightFunction);

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
