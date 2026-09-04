using System.Collections.Generic;
using System.Diagnostics;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Services.Log;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Reasoners
{
    /// <summary>
    ///     A decision maker that chooses among a set of options and keeps one selected until something ends it. Each
    ///     cycle it rescores every option and lets the derived reasoner choose which, if any, to run.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.3, Listing 8.3</seealso>
    public abstract class ReasonerBase : IAiComponent
    {
        private static readonly AiString OptionElement = "Option";

        private readonly List<OptionBase> _options = new();

        /// <summary>The options this reasoner chooses among, in the order they were added.</summary>
        protected IReadOnlyList<OptionBase> Options => _options;

        /// <summary>Where this reasoner reports the decisions it makes.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>The character this reasoner decides for, which its entries are filed under.</summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>True while the reasoner is allowed to decide.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>The option this reasoner is currently running, or null if it is running none.</summary>
        public OptionBase? SelectedOption { get; private set; }

        /// <summary>
        ///     True while SelectedOption is null, since a reasoner running nothing has nothing left to finish.
        ///     <see cref="SubreasonerAction" /> reads this to report its own IsDone.
        /// </summary>
        public virtual bool IsDone => SelectedOption == null;

        /// <summary>Loads this reasoner's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!LoadOptions(creationData))
            {
                return false;
            }

            return OnInit(creationData);
        }

        /// <summary>Loads the derived reasoner's own configuration, once the shared configuration and the Option children are in.</summary>
        /// <remarks>The base reports true, so a reasoner with nothing of its own to load leaves this alone.</remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Loads the configuration every reasoner shares, the reporting services and the Option children.</summary>
        /// <remarks>
        ///     The reasoner types differ in how they decide rather than in what they hold, so Init loads this for all of them
        ///     before OnInit.
        /// </remarks>
        private bool LoadOptions(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            LogSink = creationData.LogSink;
            LogChannel = creationData.LogChannel;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != OptionElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (!creationData.Factories.Options.TryCreate(creationData.ForChild(child), out OptionBase? option))
                {
                    return false;
                }

                AddOption(option!);
            }

            return true;
        }

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

        /// <summary>Enables the reasoner. Called by <see cref="SubreasonerAction.Select" /> and <see cref="Brain.Enable" />.</summary>
        public void Enable()
        {
            IsEnabled = true;
            OnEnable();
        }

        /// <summary>Called after the reasoner is enabled, so that subclasses can reset whatever state a run starts from.</summary>
        protected virtual void OnEnable()
        {
        }

        /// <summary>Disables the reasoner. Called by <see cref="SubreasonerAction.Deselect" /> and <see cref="Brain.Disable" />.</summary>
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

            if (SelectedOption != null)
            {
                LogSink.LogMessage(LogChannel, $"deselected {SelectedOption.Name.Text}");
            }

            SelectedOption?.Deselect();
            SelectedOption = option;

            if (option != null)
            {
                LogSink.LogMessage(LogChannel, $"selected {option.Name.Text}");
            }

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
