using System;
using Gaia.Core.Factories;
using Gaia.Core.Options;

namespace Gaia.Core.Reasoners
{
    /// <summary>
    ///     A reasoner that runs its options one at a time, in the order they were added, moving on when the selected
    ///     option's <see cref="OptionBase.IsDone" /> is true. It behaves much like a sequence node in a behavior tree. Every
    ///     option is executed when its turn comes, so the weights the considerations report play no part in which option runs.
    /// </summary>
    /// <remarks>
    ///     Each option of a sequence is one step, and an instantaneous step is one done the moment it is selected. A run
    ///     of instantaneous steps is passed in a single cycle rather than costing a cycle apiece. The rule that makes that
    ///     safe lives on <see cref="Actions.ActionBase.IsDone" />. An instantaneous action completes its work in OnSelect.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.3</seealso>
    public class SequenceReasoner : ReasonerBase
    {
        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.Reasoners,
            "Runs its options one after another, each until it is done.", Array.Empty<AttributeDescription>(),
            new[] { OptionSlot });

        /// <summary>The position in the sequence, the index of the running option or of the next one to start.</summary>
        private int _currentIndex;

        /// <summary>True once the sequence has run off its last step. An empty sequence is done from the start.</summary>
        /// <remarks>
        ///     Done is the position, not the selection, so a step held by a cycle with nothing eligible leaves the sequence
        ///     not done and a parent cannot mistake that for completion.
        /// </remarks>
        public override bool IsDone => _currentIndex >= OptionList.Count;

        /// <summary>The same as IsDone, since a sequence's selection is finished only once the sequence is.</summary>
        /// <remarks>
        ///     The sequence selects each step in turn, so its selected option being done finishes only that step. A
        ///     <see cref="Actions.SubreasonerAction" /> holding the sequence would otherwise report done as soon as one step
        ///     finished, before the sequence moved on to the next.
        /// </remarks>
        internal override bool IsSelectionDone => IsDone;

        /// <summary>Starts the sequence over, so that being enabled again runs it from the top.</summary>
        protected override void OnEnable()
        {
            _currentIndex = 0;
        }

        /// <inheritdoc />
        protected override void Think()
        {
            // Stay with SelectedOption until its IsDone is true. This is the only reasoner whose decision depends on
            // what it chose last cycle rather than only on the current situation.
            if (SelectedOption != null && !SelectedOption.IsDone)
            {
                return;
            }

            // An instantaneous step is done the moment it is selected, so stepping keeps going until it reaches a step
            // with work left or runs out of options. A step passed this way receives Select and Deselect with no Update
            // between, which is why an instantaneous action completes its work in OnSelect. The option that just
            // finished is the one at _currentIndex, and nothing selected means the sequence has either not started or
            // already run off its end, neither of which is a step to move past.
            do
            {
                if (SelectedOption != null)
                {
                    _currentIndex++;
                }

                // Running off the end leaves the index past the last option, which is what makes IsDone report
                // true. The sequence stays finished until it is enabled again rather than looping.
                SelectOption(_currentIndex < OptionList.Count ? OptionList[_currentIndex] : null);
            } while (SelectedOption is { IsDone: true });
        }
    }
}
