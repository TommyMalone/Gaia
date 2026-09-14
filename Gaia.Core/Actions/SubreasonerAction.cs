using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Actions
{
    /// <summary>An action that holds a reasoner of its own. This allows a flat decision to become a hierarchy.</summary>
    /// <remarks>
    ///     The contained reasoner runs only while the option holding this action is selected. Selecting the action
    ///     enables it, deselecting it disables it, and disabling a reasoner deselects whatever it was running. A single veto
    ///     near the root therefore tears down the whole branch beneath it. Each level's reasoner is disabled, which deselects
    ///     the option it was running, which deselects that option's actions, considerations, and any subreasoners the actions
    ///     hold.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.4</seealso>
    public sealed class SubreasonerAction : ActionBase
    {
        private static readonly AiString ReasonerElement = "Reasoner";

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

        /// <summary>True once the reasoner has run out of work or the option it selected is done. Null means done at once.</summary>
        /// <remarks>
        ///     The step ends when what the reasoner chose has finished, and not while nothing is selected, since a cycle with
        ///     nothing eligible is not completion. The reasoner says when what it chose has finished through IsSelectionDone. For
        ///     a scoring reasoner that is its selected option being done, and for a <see cref="Reasoners.SequenceReasoner" /> it
        ///     is the sequence running off its last step, since the steps follow one another rather than being chosen.
        /// </remarks>
        public override bool IsDone => Reasoner == null || Reasoner.IsDone || Reasoner.IsSelectionDone;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != ReasonerElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (!creationData.Factories.Reasoners.TryCreate(creationData.ForChild(child),
                        out ReasonerBase? reasoner))
                {
                    return false;
                }

                Reasoner = reasoner;
            }

            if (Reasoner == null)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.Name.Text} element has no Reasoner element");
                return false;
            }

            return true;
        }

        /// <summary>Calls Enable on Reasoner so that it begins making decisions.</summary>
        protected override void OnSelect()
        {
            Reasoner?.Enable();
        }

        /// <summary>Calls Disable on Reasoner, which deselects whatever option it was running.</summary>
        protected override void OnDeselect()
        {
            Reasoner?.Disable();
        }

        /// <summary>Calls Update on Reasoner, so that every level of the tree decides once per cycle.</summary>
        protected override void OnUpdate()
        {
            Reasoner?.Update();
        }
    }
}
