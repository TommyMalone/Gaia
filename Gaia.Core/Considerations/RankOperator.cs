namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     How the ranks of the considerations a <see cref="GroupConsideration" /> holds combine, read from the group's
    ///     RankOperator attribute.
    /// </summary>
    /// <remarks>
    ///     A consideration that reports no rank, <see cref="WeightFunctions.WeightValues.NoRank" />, is left out, and a
    ///     group none of whose considerations reports one reports no rank. Under Or the ranks read are those of the
    ///     considerations Or counts.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public enum RankOperator
    {
        /// <summary>
        ///     The highest rank. A group with no RankOperator combines this way, as an option combines its own
        ///     considerations.
        /// </summary>
        Max,

        /// <summary>The lowest rank.</summary>
        Min,

        /// <summary>The ranks added together.</summary>
        Sum,
    }
}
