using System.Collections.Generic;
using System.Diagnostics;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Services.Diagnostics;
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

        /// <summary>Describes the Option children Init loads on every reasoner, for a derived reasoner's description to include.</summary>
        internal static readonly SlotDescription OptionSlot = new(OptionElement, "Option",
            ConceptualAbstraction.Options, 0, SlotDescription.Unbounded);

        /// <summary>The options this reasoner chooses among, in the order they were added.</summary>
        protected IReadOnlyList<OptionBase> Options => OptionList;

        /// <summary>The same options as their concrete list, for the loops that run every cycle.</summary>
        /// <remarks>
        ///     Iterating a List through IReadOnlyList boxes its enumerator, one small heap allocation per loop, which a
        ///     decision cycle runs several of. Iterating the concrete list keeps the enumerator on the stack, so per-cycle code
        ///     loops over this and everything else reads Options. It is protected so a game's own reasoner loops the same way, and
        ///     internal as well because the monitors read it and cannot reach Options, which is protected alone. Neither tier puts
        ///     a reasoner's options on the public surface.
        /// </remarks>
        protected internal List<OptionBase> OptionList { get; } = new();

        /// <summary>The monitor recording this reasoner's decisions, or null when nothing is watching.</summary>
        /// <remarks>
        ///     Diagnostics are attached from code by a host, a <see cref="BrainMonitor" /> for a whole brain or this for one
        ///     reasoner, and never from configuration, since how closely a run is watched is a property of the run rather than of
        ///     the character. The setter is public for that reason. A <see cref="BrainMonitor" /> attached afterward replaces a
        ///     monitor set here by hand, so a host uses one or the other. A reasoner with no monitor pays one null check per
        ///     cycle.
        /// </remarks>
        public ReasonerMonitor? Monitor { get; set; }

        /// <summary>Where this reasoner reports the decisions it makes.</summary>
        /// <remarks>
        ///     The setter is public, as it is on <see cref="Brains.Brain" /> and <see cref="Actions.ActionBase" />, because a
        ///     host assembling a tree by hand has no factory to inject the services. Loading stamps them from the creation data,
        ///     so a loaded tree needs no hand wiring.
        /// </remarks>
        public ILogSink LogSink { get; set; } = new TraceLogSink();

        /// <summary>The character this reasoner decides for, which its entries are filed under.</summary>
        public AiString LogChannel { get; set; } = AiString.Empty;

        /// <summary>True while the reasoner is allowed to decide.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        ///     The roll in [0, 1) that decided the current cycle, or 0 on a cycle with nothing rolled. A reasoner that rolls
        ///     sets it from Think, and its monitor reads it.
        /// </summary>
        protected internal float LastDecisionRoll { get; set; }

        /// <summary>The option this reasoner is currently running, or null if it is running none.</summary>
        public OptionBase? SelectedOption { get; private set; }

        /// <summary>True once this reasoner has run out of work. The base reports false, since a scoring reasoner never finishes.</summary>
        /// <remarks>
        ///     Done means ran out of work, never nothing was eligible this cycle. A scoring reasoner chooses by the weights
        ///     its options report, which a <see cref="RuleBasedReasoner" /> and a <see cref="DualUtilityReasoner" /> do, and a
        ///     <see cref="SequenceReasoner" /> chooses by position. Selecting nothing is a circumstance, such as every option
        ///     vetoed for a cycle while the host rewrites a list, and treating it as completion would let one such cycle advance a
        ///     parent SequenceReasoner past unfinished work. A reasoner with an end overrides this, a sequence once it is past its
        ///     last step. <see cref="SubreasonerAction" /> applies the completion rule its step was configured with, by default
        ///     done when the option this reasoner selected has finished, which IsSelectionDone reports. A scoring reasoner's own
        ///     option ends the way every option ends, through its considerations.
        /// </remarks>
        public virtual bool IsDone => false;

        /// <summary>
        ///     True once this reasoner's selected option is done. A <see cref="SubreasonerAction" /> under its default
        ///     SelectedOptionDone rule reports done when this is true.
        /// </summary>
        /// <remarks>
        ///     A reasoner that runs several of its options in turn, as a <see cref="SequenceReasoner" /> does, overrides this
        ///     so that the step ends only when the last of them is done.
        /// </remarks>
        internal virtual bool IsSelectionDone => SelectedOption is { IsDone: true };

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

            WarnWhenTwoOptionsShareAName();

            return true;
        }

        /// <summary>Warns for each pair of loaded options carrying the same name.</summary>
        /// <remarks>
        ///     A log line and a decision record identify an option by its name alone, so two options sharing one cannot be
        ///     told apart in either. Loading accepts it, since nothing about the decision itself is wrong.
        /// </remarks>
        private void WarnWhenTwoOptionsShareAName()
        {
            for (int first = 0; first < OptionList.Count; first++)
            {
                for (int second = first + 1; second < OptionList.Count; second++)
                {
                    if (OptionList[first].Name == OptionList[second].Name && LogSink.IsEnabled(LogSeverity.Warning))
                    {
                        LogSink.LogWarning(LogChannel,
                            $"two options share the name {OptionList[first].Name.Text}, their log lines and records cannot be told apart");
                    }
                }
            }
        }

        /// <summary>Adds an option to the current set of options.</summary>
        /// <param name="option">The option to add.</param>
        public void AddOption(OptionBase option)
        {
            OptionList.Add(option);
        }

        /// <summary>Clears the current set of options.</summary>
        public void Clear()
        {
            OptionList.Clear();
        }

        /// <summary>Enables the reasoner. Called by <see cref="SubreasonerAction.Select" /> and <see cref="Brain.Enable" />.</summary>
        public void Enable()
        {
            IsEnabled = true;

            // Loading supplies each option's default tuning consideration from Init. An option assembled in code never
            // runs Init, and by the time its reasoner is enabled the assembler has added the considerations it means
            // to, so the default is supplied here instead. An option that already has one is left alone.
            foreach (OptionBase option in OptionList)
            {
                option.EnsureTuningConsideration();
            }

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

            Debug.Assert(option == null || OptionList.Contains(option),
                "The option is not one of this reasoner's options.");

            if (SelectedOption != null && LogSink.IsEnabled(LogSeverity.Message))
            {
                LogSink.LogMessage(LogChannel, $"deselected {SelectedOption.Name.Text}");
            }

            SelectedOption?.Deselect();
            SelectedOption = option;

            if (option != null && LogSink.IsEnabled(LogSeverity.Message))
            {
                LogSink.LogMessage(LogChannel, $"selected {option.Name.Text}");
            }

            SelectedOption?.Select();
        }

        /// <summary>
        ///     Runs one decision cycle. Sense is the perception phase and is empty, EvaluateOptions rescores every option,
        ///     Think chooses which to run, and Act updates whatever is selected.
        /// </summary>
        public void Update()
        {
            if (IsEnabled)
            {
                LastDecisionRoll = 0f;

                Sense();
                EvaluateOptions();
                Think();
                Monitor?.OnCycle(this);
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
            foreach (OptionBase option in OptionList)
            {
                option.Calculate();
            }
        }
    }
}
