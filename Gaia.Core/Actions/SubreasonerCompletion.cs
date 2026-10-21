namespace Gaia.Core.Actions
{
    /// <summary>The rule for when a <see cref="SubreasonerAction" /> reports done, read from the action's DoneWhen attribute.</summary>
    /// <remarks>Under every rule the action is done once its reasoner's <see cref="Reasoners.ReasonerBase.IsDone" /> is true.</remarks>
    public enum SubreasonerCompletion
    {
        /// <summary>
        ///     Done once the option the reasoner selected is done. A SequenceReasoner's selection counts as done only once
        ///     the sequence has run off its last step, since its steps follow one another rather than being chosen.
        /// </summary>
        SelectedOptionDone,

        /// <summary>
        ///     Done once the reasoner has selected nothing for <see cref="SubreasonerAction.NothingEligibleCycleLimit" />
        ///     consecutive cycles, as it does while every option is vetoed or scores nothing.
        /// </summary>
        NothingEligible,

        /// <summary>
        ///     Done only when the reasoner reports IsDone, which a <see cref="Reasoners.RuleBasedReasoner" /> or
        ///     <see cref="Reasoners.DualUtilityReasoner" /> never does.
        /// </summary>
        ReasonerDone,
    }
}
