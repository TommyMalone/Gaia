using System;
using Gaia.Core.Factories;
using Gaia.Core.Options;

namespace Gaia.Core.Reasoners
{
    /// <summary>A reasoner that selects the first valid option in the order the options were added.</summary>
    /// <seealso>"Modular AI", Section 8.5.3</seealso>
    public class RuleBasedReasoner : ReasonerBase
    {
        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.Reasoners,
            "Picks the first option, in written order, that is not ruled out.", Array.Empty<AttributeDescription>(),
            new[] { OptionSlot });

        /// <inheritdoc />
        protected override void Think()
        {
            OptionBase? chosenOption = null;

            foreach (OptionBase option in OptionList)
            {
                if (option.IsValid)
                {
                    chosenOption = option;
                    break;
                }
            }

            SelectOption(chosenOption);
        }
    }
}
