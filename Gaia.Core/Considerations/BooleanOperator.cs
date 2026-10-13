namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     Which of the considerations a <see cref="GroupConsideration" /> holds must allow its option, read from the
    ///     group's BooleanOperator attribute.
    /// </summary>
    /// <remarks>
    ///     A consideration allows the option when its multiplier is above 0 and vetoes it at 0 or below. The ranks
    ///     combine under the group's RankOperator whichever operator it uses.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public enum BooleanOperator
    {
        /// <summary>
        ///     Allowed when every consideration held allows it. The addends are summed and the multipliers multiplied. A
        ///     group with no BooleanOperator combines this way, as an option combines its own considerations.
        /// </summary>
        And,

        /// <summary>Allowed when at least one consideration held allows it.</summary>
        /// <remarks>
        ///     A consideration that vetoes is left out, its addend, multiplier and rank with it. Of those that allow, the
        ///     addends are summed and the highest multiplier is kept, one above 1 included, so a second reason never weakens the
        ///     group. When every one vetoes, nothing is left out and the group vetoes.
        /// </remarks>
        Or,

        /// <summary>Allowed when the one consideration held vetoes the option, and vetoed when it allows.</summary>
        /// <remarks>
        ///     A Not group holds exactly one consideration, which may be a group, so a Not holding an Or group allows the
        ///     option when none of the Or group's considerations does. A consideration that only weakens the option counts as
        ///     allowing it. Its addend and rank pass through.
        /// </remarks>
        Not,
    }
}
