using Gaia.Core.Temp;

namespace Gaia.Core.Considerations
{
    public sealed class ConsiderationSetConsideration : ConsiderationBase
    {
        private readonly ConsiderationSet _set = new();

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            return true;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            _set.Calculate();
            Weights = new WeightValues(_set.Addend, _set.Multiplier, _set.UnscreenedRank);
        }
    }
}
