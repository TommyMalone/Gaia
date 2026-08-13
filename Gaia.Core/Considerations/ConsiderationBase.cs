using Gaia.Core.Options;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     One thing an option takes into account. Each cycle a consideration looks at the situation and reports an
    ///     addend, a multiplier and a rank, and the option's <see cref="ConsiderationSet" /> combines those together with what
    ///     every other consideration on the option said.
    /// </summary>
    /// <remarks>
    ///     The addend raises or lowers the option's weight, the multiplier scales it, and a multiplier of 0 is a veto
    ///     that removes the option from the running entirely. The rank is a priority class that a reasoner may settle before
    ///     comparing weights at all. Considerations are meant to be small and reusable.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.1, Listing 8.1</seealso>
    public abstract class ConsiderationBase
    {
        /// <summary>The addend, multiplier and rank this consideration reported when Calculate last ran.</summary>
        public WeightValues Weights { get; protected set; } = WeightValues.Neutral;

        /// <summary>True while the option this consideration belongs to is the one its reasoner has selected.</summary>
        public bool IsSelected { get; private set; }

        /// <summary>The option this consideration belongs to, or null until AttachTo has been called.</summary>
        /// <remarks>
        ///     Most considerations never look at this. It exists for the few that evaluate the option itself rather than the
        ///     world, such as <see cref="ExecutionHistoryConsideration" /> reading the option's <see cref="OptionBase.IsDone" />.
        /// </remarks>
        public OptionBase? Option { get; private set; }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public abstract bool Init(AiCreationData creationData);

        /// <summary>Called once per decision cycle to evaluate the situation and store the result in Weights.</summary>
        public abstract void Calculate();

        /// <summary>Sets Option. Called by the <see cref="ConsiderationSet" /> that takes this consideration.</summary>
        internal virtual void AttachTo(OptionBase option)
        {
            Option = option;
        }

        /// <summary>Called when the option this consideration scores is selected. Sets IsSelected.</summary>
        public virtual void Select()
        {
            IsSelected = true;
        }

        /// <summary>Called when the option this consideration scores is deselected. Clears IsSelected.</summary>
        public virtual void Deselect()
        {
            IsSelected = false;
        }
    }
}
