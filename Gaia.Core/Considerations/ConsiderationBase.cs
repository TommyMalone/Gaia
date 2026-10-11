using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     One thing an option takes into account. Each cycle a consideration looks at the situation and reports an
    ///     addend, a multiplier and a rank, and the option's <see cref="ConsiderationSet" /> combines those together with what
    ///     every other consideration on the option said.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The addend raises or lowers the option's weight, the multiplier scales it, and a multiplier of 0 is a veto
    ///         that removes the option from selection under any reasoner that selects by weight. The rank is a priority class
    ///         that a reasoner may settle before comparing weights at all. Considerations are meant to be small and reusable.
    ///     </para>
    ///     <para>
    ///         A shipped consideration is made by loading rather than by construction from game code, since its values load
    ///         through Init and its setters are internal. Loading through <see cref="Factories.GaiaLoader" /> is the public
    ///         way to make one. A game's own derived considerations belong to the game and expose whatever constructors it
    ///         wants.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.1, Listing 8.1</seealso>
    public abstract class ConsiderationBase : IAiComponent
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

        /// <summary>Loads this consideration's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived consideration's own configuration.</summary>
        /// <remarks>
        ///     The factory has already refused whatever the description does not name, and the default returns true, so a
        ///     consideration with nothing of its own to load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Called once per decision cycle to evaluate the situation and store the result in Weights.</summary>
        public abstract void Calculate();

        /// <summary>
        ///     Sets Option, then calls OnAttach. Called by the <see cref="ConsiderationSet" /> that takes this consideration,
        ///     and by AttachChild.
        /// </summary>
        internal void AttachTo(OptionBase option)
        {
            Option = option;
            OnAttach(option);
        }

        /// <summary>
        ///     Called by AttachTo once Option is set, so a consideration that holds considerations of its own can pass the
        ///     option to each of them through AttachChild.
        /// </summary>
        /// <remarks>A consideration this one takes on after this has run is attached by calling AttachChild with Option.</remarks>
        protected virtual void OnAttach(OptionBase option)
        {
        }

        /// <summary>Attaches a consideration this one holds to the given option, setting its Option and running its OnAttach.</summary>
        /// <remarks>
        ///     It is static because a consideration in a game's own code may call a protected instance member only on its own
        ///     type, not on a ConsiderationBase it holds.
        /// </remarks>
        protected static void AttachChild(ConsiderationBase child, OptionBase option)
        {
            child.AttachTo(option);
        }

        /// <summary>Called when the option this consideration scores is selected. Sets IsSelected, then OnSelect runs.</summary>
        public void Select()
        {
            IsSelected = true;
            OnSelect();
        }

        /// <summary>Called by Select once IsSelected is set, so a derived consideration can act on the moment.</summary>
        protected virtual void OnSelect()
        {
        }

        /// <summary>Called when the option this consideration scores is deselected. Clears IsSelected, then OnDeselect runs.</summary>
        public void Deselect()
        {
            IsSelected = false;
            OnDeselect();
        }

        /// <summary>Called by Deselect once IsSelected is cleared, so a derived consideration can act on the moment.</summary>
        protected virtual void OnDeselect()
        {
        }
    }
}
