using System.Collections.Generic;
using System.Diagnostics;
using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Temp;

namespace Gaia.Core.Reasoners
{
    /// <summary>
    ///     A decision maker that chooses among a set of options and keeps one selected until something ends it. Each
    ///     cycle it rescores every option and lets the derived reasoner choose which, if any, to run.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.3, Listing 8.3</seealso>
    public abstract class ReasonerBase
    {
        private readonly List<OptionBase> _options = new();

        /// <summary>The options this reasoner chooses among, in the order they were added.</summary>
        protected IReadOnlyList<OptionBase> Options => _options;

        /// <summary>True while the reasoner is allowed to decide.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>The option this reasoner is currently running, or null if it is running none.</summary>
        public OptionBase? SelectedOption { get; private set; }

        /// <summary>
        ///     True while SelectedOption is null, since a reasoner running nothing has nothing left to finish.
        ///     <see cref="SubreasonerAction" /> reads this to report its own IsDone.
        /// </summary>
        public virtual bool IsDone => SelectedOption == null;

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public abstract bool Init(AiCreationData creationData);

        /// <summary>Adds an option to the current set of options.</summary>
        /// <param name="option">The option to add.</param>
        public void AddOption(OptionBase option)
        {
            _options.Add(option);
        }

        /// <summary>Clears the current set of options.</summary>
        public void Clear()
        {
            _options.Clear();
        }

        /// <summary>
        ///     Enables the reasoner. Called by <see cref="SubreasonerAction.Select" /> and Brain.Enable.
        /// </summary>
        public void Enable()
        {
            IsEnabled = true;
            OnEnable();
        }

        /// <summary>Called after the reasoner is enabled, so that subclasses can reset whatever state a run starts from.</summary>
        protected virtual void OnEnable()
        {
        }

        /// <summary>
        ///     Disables the reasoner. Called by <see cref="SubreasonerAction.Deselect" /> and
        ///     Brain.Disable.
        /// </summary>
        public void Disable()
        {
            IsEnabled = false;
            SelectOption(null);
            OnDisable();
        }

        /// <summary>
        ///     Called after the reasoner is disabled and SelectedOption has been deselected, so that subclasses can clean up
        ///     whatever state a run leaves behind.
        /// </summary>
        protected virtual void OnDisable()
        {
        }

        /// <summary>Sets SelectedOption. If null no option is selected.</summary>
        /// <param name="option">The option to select or null.</param>
        protected void SelectOption(OptionBase? option)
        {
            if (ReferenceEquals(option, SelectedOption))
            {
                return;
            }

            Debug.Assert(option == null || _options.Contains(option),
                "The option is not one of this reasoner's options.");

            SelectedOption?.Deselect();
            SelectedOption = option;
            SelectedOption?.Select();
        }

        /// <summary>
        ///     Runs one decision cycle. Sense observes the world, EvaluateOptions rescores every option, Think chooses which
        ///     to run, and Act updates whatever is selected.
        /// </summary>
        public void Update()
        {
            if (IsEnabled)
            {
                Sense();
                EvaluateOptions();
                Think();
                Act();
            }
        }

        /// <summary>The perception phase of the cycle. Nothing is sensed yet, so the method is empty.</summary>
        protected void Sense()
        {
        }

        /// <summary>The decision phase of the cycle. Each reasoner overrides this to choose an option and hand it to SelectOption.</summary>
        protected virtual void Think()
        {
        }

        /// <summary>The acting phase of the cycle. Calls Update on SelectedOption so that its actions run.</summary>
        protected void Act()
        {
            SelectedOption?.Update();
        }

        /// <summary>Calls Calculate on every option in Options.</summary>
        private void EvaluateOptions()
        {
            foreach (OptionBase option in Options)
            {
                option.Calculate();
            }
        }
    }
}
