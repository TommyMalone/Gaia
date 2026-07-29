using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    public sealed class ConsiderationSetConsideration : ConsiderationBase
    {
        private readonly ConsiderationSet _set = new();

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            _set.Calculate();
            Weights = new WeightValues(_set.Addend, _set.Multiplier, _set.UnscreenedRank);
        }
    }
}
