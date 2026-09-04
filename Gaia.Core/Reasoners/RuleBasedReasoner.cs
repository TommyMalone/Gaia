using Gaia.Core.Options;

namespace Gaia.Core.Reasoners
{
    /// <summary>A reasoner that selects the first valid option in the order the options were added.</summary>
    /// <seealso>"Modular AI", Section 8.5.3</seealso>
    public class RuleBasedReasoner : ReasonerBase
    {
        /// <inheritdoc />
        protected override void Think()
        {
            OptionBase? chosenOption = null;

            foreach (OptionBase option in Options)
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
