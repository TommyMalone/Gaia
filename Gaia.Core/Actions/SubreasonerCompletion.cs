namespace Gaia.Core.Actions
{
    /// <summary>The rule for when a <see cref="SubreasonerAction" /> reports done, read from the action's DoneWhen attribute.</summary>
    /// <remarks>
    ///     The action's done state counts toward the <see cref="Options.OptionBase.IsDone" /> of its option. A parent
    ///     <see cref="Reasoners.SequenceReasoner" /> moves past a done option, and under any reasoner an
    ///     <see cref="Considerations.ExecutionHistoryConsideration" /> weighs its option through its FinishedWeightFunction
    ///     once the option is done. Under every rule the action is done once its reasoner's
    ///     <see cref="Reasoners.ReasonerBase.IsDone" /> is true.
    /// </remarks>
    public enum SubreasonerCompletion
    {
        /// <summary>
        ///     Done once the option the reasoner selected is done. A SequenceReasoner's selection counts as done only once
        ///     the sequence has run off its last step, since its steps follow one another rather than being chosen.
        /// </summary>
        SelectedOptionDone,

        /// <summary>
        ///     Done once nothing has been eligible for <see cref="SubreasonerAction.NothingEligibleCycleLimit" /> consecutive
        ///     cycles, which is what every option vetoed or scoring nothing leaves the reasoner with. That limit is 1 or more, and
        ///     1 by default.
        /// </summary>
        NothingEligible,

        /// <summary>
        ///     Done only when the reasoner reports IsDone, which a <see cref="Reasoners.RuleBasedReasoner" /> or
        ///     <see cref="Reasoners.DualUtilityReasoner" /> never does.
        /// </summary>
        ReasonerDone,
    }
}
