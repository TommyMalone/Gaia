using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Specifications;

namespace Gaia.Core.Actions
{
    /// <summary>An action that holds a reasoner of its own. This allows a flat decision to become a hierarchy.</summary>
    /// <remarks>
    ///     A veto near the root, under a reasoner that selects by weight, reaches every level beneath it. Deselecting an
    ///     option deselects its actions and considerations, each subreasoner action among them disables the reasoner it holds,
    ///     and that reasoner deselects the option it was running.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.4</seealso>
    public sealed class SubreasonerAction : ActionBase
    {
        private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Actions,
            "Runs a reasoner of its own while selected, which nests one decision inside another.").Add(NameAttribute);

        private static readonly AttributeDescription DoneWhenAttribute =
            Builder.Enumeration<SubreasonerCompletion>("DoneWhen", nameof(SubreasonerCompletion.SelectedOptionDone),
                "When the action reports done.");

        private static readonly AttributeDescription NothingEligibleCycleLimitAttribute = Builder.Optional(
            "NothingEligibleCycleLimit", AttributeKind.Int, "1",
            "How many cycles in a row with nothing eligible end the action, under the NothingEligible rule alone.");

        private static readonly SlotDescription ReasonerSlot =
            Builder.Slot("Reasoner", ConceptualAbstraction.Reasoners, 1, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Counted for the NothingEligible rule, and restarted by any cycle that selects something.</summary>
        private int _consecutiveCyclesNothingHasBeenEligible;

        /// <summary>Creates an action with no reasoner, which does nothing until a configuration loads one.</summary>
        internal SubreasonerAction()
        {
        }

        /// <summary>Creates an action that runs the given reasoner while this action is selected.</summary>
        internal SubreasonerAction(ReasonerBase reasoner)
        {
            Reasoner = reasoner;
        }

        /// <summary>The reasoner this action runs. Null means the action does nothing and IsDone is immediately true.</summary>
        public ReasonerBase? Reasoner { get; internal set; }

        /// <summary>When this action reports done, loaded from the DoneWhen attribute. Defaults to SelectedOptionDone.</summary>
        /// <remarks>
        ///     The rule is on the action rather than on the reasoner, because the same subtree can be wanted two ways. One
        ///     sequence wants its step to end as soon as the option the reasoner chose has finished, and another wants it to run
        ///     until nothing is eligible.
        /// </remarks>
        public SubreasonerCompletion DoneWhen { get; internal set; } = SubreasonerCompletion.SelectedOptionDone;

        /// <summary>
        ///     How many consecutive cycles with nothing eligible end the step under the NothingEligible rule, loaded from the
        ///     NothingEligibleCycleLimit attribute. Defaults to 1 and must be at least 1.
        /// </summary>
        /// <remarks>
        ///     The attribute is refused under any other rule, since no other rule reads it. A cycle can have nothing eligible
        ///     while work remains, as when every option is vetoed by a consideration reading an entity list the host is partway
        ///     through rewriting. A limit above 1 keeps one such cycle from ending the step. The count restarts whenever the
        ///     reasoner selects something. A limit at or past the monitor's
        ///     <see cref="MonitorSettings.NothingEligibleForSelectionWarningWindow" /> makes the monitor log that warning before
        ///     the step ends, since the monitor reads a scoring reasoner with nothing selected as one with work left.
        /// </remarks>
        public int NothingEligibleCycleLimit { get; internal set; } = 1;

        /// <summary>True once the reasoner has run out of work, or once the DoneWhen rule is met.</summary>
        /// <remarks>
        ///     Nothing selected is never read as done on its own, since a cycle with nothing eligible is not completion, and
        ///     only the NothingEligible rule turns a run of such cycles into an end.
        /// </remarks>
        public override bool IsDone
        {
            get
            {
                if (Reasoner == null || Reasoner.IsDone)
                {
                    return true;
                }

                switch (DoneWhen)
                {
                    case SubreasonerCompletion.SelectedOptionDone:
                        return Reasoner.IsSelectionDone;
                    case SubreasonerCompletion.NothingEligible:
                        return _consecutiveCyclesNothingHasBeenEligible >= NothingEligibleCycleLimit;
                    default:
                        // ReasonerDone. The check above the switch is the whole of that rule.
                        return false;
                }
            }
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            SubreasonerCompletion doneWhen = DoneWhen;

            if (node.HasAttribute(DoneWhenAttribute.Name) &&
                !node.ReadEnumeration(DoneWhenAttribute.Name, out doneWhen))
            {
                return false;
            }

            DoneWhen = doneWhen;

            if (node.HasAttribute(NothingEligibleCycleLimitAttribute.Name))
            {
                if (DoneWhen != SubreasonerCompletion.NothingEligible)
                {
                    creationData.ReportError(
                        $"Line {node.LineNumber}, the {node.NameSpelling} element sets NothingEligibleCycleLimit under the {DoneWhen} rule");
                    return false;
                }

                if (!node.ReadInt(NothingEligibleCycleLimitAttribute.Name, out int nothingEligibleCycleLimit))
                {
                    return false;
                }

                if (nothingEligibleCycleLimit < 1)
                {
                    node.TryGetString(NothingEligibleCycleLimitAttribute.Name,
                        out string nothingEligibleCycleLimitText);
                    node.ReportMalformed(NothingEligibleCycleLimitAttribute.Name, nothingEligibleCycleLimitText,
                        "1 or more");
                    return false;
                }

                NothingEligibleCycleLimit = nothingEligibleCycleLimit;
            }

            // Every child is the one Reasoner, since the factory refused anything else against the description, and
            // the factory refuses an element with none once this returns.
            foreach (SpecificationNode child in node.Children)
            {
                if (!creationData.Factories.Reasoners.TryCreate(creationData.ForChild(child),
                        out ReasonerBase? reasoner))
                {
                    return false;
                }

                Reasoner = reasoner;
            }

            return true;
        }

        /// <summary>Calls Enable on Reasoner so that it begins making decisions, and restarts the nothing-eligible count.</summary>
        protected override void OnSelect()
        {
            _consecutiveCyclesNothingHasBeenEligible = 0;
            Reasoner?.Enable();
        }

        /// <summary>Calls Disable on Reasoner, which deselects whatever option it was running.</summary>
        protected override void OnDeselect()
        {
            Reasoner?.Disable();
        }

        /// <summary>
        ///     Calls Update on Reasoner, so that every level of the tree decides once per cycle, and counts the cycles on
        ///     which it selected nothing.
        /// </summary>
        protected override void OnUpdate()
        {
            if (Reasoner == null)
            {
                return;
            }

            Reasoner.Update();
            _consecutiveCyclesNothingHasBeenEligible = Reasoner.SelectedOption == null
                ? _consecutiveCyclesNothingHasBeenEligible + 1
                : 0;
        }
    }
}
