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
        /// <summary>Describes the Option children every reasoner's Init reads by, for a derived reasoner's description to add.</summary>
        public static readonly SlotDescription OptionSlot = new("Option", "Option", ConceptualAbstraction.Options, 0,
            SlotDescription.Unbounded);

        /// <summary>The options this reasoner chooses among, in the order they were added.</summary>
        /// <remarks>
        ///     A read-only view of the same list MutableOptions holds, so a reader outside the library reaches a loaded
        ///     reasoner's options without reaching the list itself. A foreach over this list allocates a boxed enumerator each
        ///     time it runs and an indexed for does not, so a host that reads it every tick loops by index.
        /// </remarks>
        public IReadOnlyList<OptionBase> Options => MutableOptions;

        /// <summary>The same options as their concrete list, for the loops that run every cycle.</summary>
        /// <remarks>
        ///     Iterating a List through IReadOnlyList boxes its enumerator, one small heap allocation per loop, and a
        ///     decision cycle runs several loops. Iterating the concrete list keeps the enumerator on the stack, so the library
        ///     loops over this and Options is the view a reader outside it gets. It is protected so a game's own reasoner can loop
        ///     the concrete list too, and internal as well so the monitors, which record inside the cycle, loop it rather than
        ///     Options. AddOption adds to this list, and a game's own reasoner may add to it directly.
        /// </remarks>
        protected internal List<OptionBase> MutableOptions { get; } = new();

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
        ///     Done means the reasoner ran out of work, not that nothing was eligible this cycle. A scoring reasoner chooses
        ///     by the weights its options report, which a <see cref="RuleBasedReasoner" /> and a
        ///     <see cref="DualUtilityReasoner" /> do, and a <see cref="SequenceReasoner" /> chooses by position. Selecting nothing
        ///     is a circumstance, such as every option vetoed for a cycle while the host rewrites a list, and treating it as
        ///     completion would let one such cycle advance a parent SequenceReasoner past unfinished work. A reasoner with an end
        ///     overrides this, a sequence once it is past its last step. A scoring reasoner never reads an option's IsDone in its
        ///     decision, and only a SequenceReasoner advances on it. An option a scoring reasoner selected ends when its
        ///     considerations veto it or score another option above it, and a finished option ends through the finished veto, an
        ///     <see cref="Considerations.ExecutionHistoryConsideration" /> whose Finished weight function reports Vetoed once the
        ///     option's IsDone is true.
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

            // Every child is an Option, since the factory refused anything else against the description.
            foreach (SpecificationNode child in node.Children)
            {
                if (!creationData.Factories.Options.TryCreate(creationData.ForChild(child), out OptionBase? option))
                {
                    return false;
                }

                WarnWhenAnEarlierOptionSharesTheName(option!, child);
                AddOption(option!);
            }

            return true;
        }

        /// <summary>Warns when a loaded option carries the name of an option loaded before it.</summary>
        /// <remarks>
        ///     A log line and a decision record identify an option by its name alone, so two options sharing one cannot be
        ///     told apart in either. Loading accepts it, since nothing about the decision itself is wrong.
        /// </remarks>
        private void WarnWhenAnEarlierOptionSharesTheName(OptionBase option, SpecificationNode node)
        {
            if (!LogSink.IsEnabled(LogSeverity.Warning))
            {
                return;
            }

            for (int index = 0; index < MutableOptions.Count; index++)
            {
                if (MutableOptions[index].Name == option.Name)
                {
                    LogSink.LogWarning(LogChannel,
                        $"Line {node.LineNumber}, the {node.NameSpelling} element shares the name {option.Name.Text} with an earlier option, their log lines and records cannot be told apart");
                    return;
                }
            }
        }

        /// <summary>Adds an option to the current set of options.</summary>
        public void AddOption(OptionBase option)
        {
            MutableOptions.Add(option);
        }

        /// <summary>Clears the current set of options.</summary>
        public void Clear()
        {
            MutableOptions.Clear();
        }

        /// <summary>Enables the reasoner. Called by <see cref="SubreasonerAction.Select" /> and <see cref="Brain.Enable" />.</summary>
        public void Enable()
        {
            IsEnabled = true;

            // An option assembled in code never runs Init, which is where a loaded option gets its default tuning
            // consideration, so it is supplied here instead.
            foreach (OptionBase option in MutableOptions)
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
        protected void SelectOption(OptionBase? option)
        {
            if (ReferenceEquals(option, SelectedOption))
            {
                return;
            }

            Debug.Assert(option == null || MutableOptions.Contains(option),
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

        /// <summary>Runs one decision cycle while the reasoner is enabled.</summary>
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

        /// <summary>The perception phase of the cycle, which presently senses nothing.</summary>
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
            foreach (OptionBase option in MutableOptions)
            {
                option.Calculate();
            }
        }
    }
}
