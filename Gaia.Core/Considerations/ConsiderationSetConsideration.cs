using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that is itself a set of considerations.</summary>
    /// <remarks>
    ///     Without it every consideration on an option sits in one flat list. With it a subset can be grouped first and
    ///     its combined answer reported as a single consideration. Under the library's one combining rule, summed addends,
    ///     multiplied multipliers and the highest rank, a grouped subset produces the same totals a flat list would, so the
    ///     grouping provides structure rather than different numbers.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public sealed class ConsiderationSetConsideration : ConsiderationBase
    {
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Considerations,
                    "Groups considerations and reports their combined values as one.")
                .Add(ConsiderationSet.ConsiderationSlot).Build();

        private readonly ConsiderationSet _set = new();

        /// <summary>Creates a consideration whose contained set starts empty until a configuration loads.</summary>
        internal ConsiderationSetConsideration() : this(Array.Empty<ConsiderationBase>())
        {
        }

        /// <summary>Creates a consideration whose contained set holds the given considerations.</summary>
        internal ConsiderationSetConsideration(IEnumerable<ConsiderationBase> considerations)
        {
            foreach (ConsiderationBase consideration in considerations)
            {
                _set.Add(consideration);
            }
        }

        /// <summary>The considerations this one groups, in the order they were added.</summary>
        /// <remarks>
        ///     The <see cref="ConsiderationSet" /> that holds them is an implementation detail of this consideration, so this
        ///     is where a reader reaches them. Nothing on the cycle path reads it, since combining runs inside the set. A foreach
        ///     over this list allocates a boxed enumerator each time it runs and an indexed for does not, so a host that reads it
        ///     every tick loops by index.
        /// </remarks>
        public IReadOnlyList<ConsiderationBase> Considerations => _set.Considerations;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            return _set.Init(creationData);
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
        protected override void OnSelect()
        {
            _set.Select();
        }

        /// <inheritdoc />
        protected override void OnDeselect()
        {
            _set.Deselect();
        }
    }
}
