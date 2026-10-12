using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A group that combines what it holds the way an option combines its own considerations.</summary>
    /// <remarks>
    ///     Without it every consideration on an option sits in one flat list. With it a subset can be grouped first and
    ///     its combined answer reported as a single consideration. Under the library's one combining rule, summed addends,
    ///     multiplied multipliers and the highest rank, a grouped subset produces the same totals a flat list would, so the
    ///     grouping provides structure rather than different numbers.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public sealed class GroupConsideration : GroupConsiderationBase
    {
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Considerations,
                "Groups considerations and reports their combined values as one.").Add(ConsiderationSlot).Build();

        /// <summary>Creates a group that holds nothing until a configuration loads.</summary>
        internal GroupConsideration()
        {
        }

        /// <summary>Creates a group that holds the given considerations.</summary>
        internal GroupConsideration(IEnumerable<ConsiderationBase> considerations) : base(considerations)
        {
        }

        /// <inheritdoc />
        protected override WeightValues CombineWeights()
        {
            return Totals;
        }
    }
}
