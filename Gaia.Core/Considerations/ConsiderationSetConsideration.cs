using System.Collections.Generic;
using Gaia.Core.Options;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that is itself a set of considerations.</summary>
    /// <remarks>
    ///     Without it every consideration on an option sits in one flat list. With it a subset can be grouped first and
    ///     its combined answer reported as a single consideration. Under the one combining rule the library presently has,
    ///     summed addends, multiplied multipliers and the highest rank, a grouped subset produces the same totals a flat list
    ///     would, so for now the grouping buys structure rather than different numbers.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public sealed class ConsiderationSetConsideration : ConsiderationBase
    {
        private readonly ConsiderationSet _set = new();

        /// <summary>Creates a consideration whose contained set holds the given considerations.</summary>
        internal ConsiderationSetConsideration(IEnumerable<ConsiderationBase> considerations)
        {
            foreach (ConsiderationBase consideration in considerations)
            {
                _set.Add(consideration);
            }
        }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            _set.Init(creationData);
            return false;
        }

        /// <summary>
        ///     Passes the option down to the contained <see cref="ConsiderationSet" /> through its AttachTo, so nested
        ///     considerations know it too.
        /// </summary>
        internal override void AttachTo(OptionBase option)
        {
            base.AttachTo(option);
            _set.AttachTo(option);
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            _set.Calculate();
            Weights = new WeightValues(_set.Addend, _set.Multiplier, _set.Rank);
        }

        /// <inheritdoc />
        public override void Select()
        {
            base.Select();
            _set.Select();
        }

        /// <inheritdoc />
        public override void Deselect()
        {
            base.Deselect();
            _set.Deselect();
        }
    }
}
