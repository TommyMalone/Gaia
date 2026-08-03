using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that reports fixed values, so that an option can be tuned by hand.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.3</seealso>
    public sealed class TuningConsideration : ConsiderationBase
    {
        /// <summary>Creates a consideration that reports neutral values.</summary>
        public TuningConsideration() : this(new WeightValues(1f, 1f, float.MinValue))
        {
        }

        public TuningConsideration(WeightValues weights)
        {
            Weights = weights;
        }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
        }
    }
}