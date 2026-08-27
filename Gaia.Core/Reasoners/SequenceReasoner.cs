using Gaia.Core.Factories;
using Gaia.Core.Options;

namespace Gaia.Core.Reasoners
{
    /// <summary>
    ///     A reasoner that runs its options one at a time, in the order they were added, moving on when the selected
    ///     option's <see cref="OptionBase.IsDone" /> is true. It behaves much like a sequence node in a behavior tree. Every
    ///     option is executed when its turn comes, so the weights the considerations report play no part in which option runs.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.3</seealso>
    public class SequenceReasoner : ReasonerBase
    {
        /// <summary>The position in the sequence, the index of the running option or of the next one to start.</summary>
        private int _currentIndex;

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

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

            // The option that just finished is the one at _currentIndex, so step past it. Nothing selected means
            // the sequence has not started yet and the first option is still to come.
            if (SelectedOption != null)
            {
                _currentIndex++;
            }

            // Running off the end selects nothing, which is what makes IsDone report true. The sequence stays
            // finished until it is enabled again rather than looping.
            SelectOption(_currentIndex < Options.Count ? Options[_currentIndex] : null);
        }
    }
}
