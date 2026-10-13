using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     A group that combines what it holds under its BooleanOperator and RankOperator, by default the way an option
    ///     combines its own considerations.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Without it every consideration on an option sits in one flat list. With it a subset can be grouped, combined
    ///         under the group's BooleanOperator and RankOperator, and reported as a single consideration. With neither
    ///         attribute the group combines as the option does, so its totals are the ones the same considerations give
    ///         written flat.
    ///     </para>
    ///     <para>
    ///         A Tuning reports a multiplier of 1 unless it is written with another. Inside an Or group one that allows
    ///         makes the group allow the option on every cycle, and one that vetoes is left out and changes nothing. As the
    ///         one consideration of a Not group a Tuning fixes the answer either way, one that allows making the group veto on
    ///         every cycle and one written with a multiplier of 0 making it allow. Loading logs a warning wherever a Tuning
    ///         fixes the answer.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public sealed class GroupConsideration : GroupConsiderationBase
    {
        private static readonly DescriptionBuilder Builder =
            new DescriptionBuilder(ConceptualAbstraction.Considerations,
                "Groups considerations and reports their combined values as one.").Add(ConsiderationSlot);

        private static readonly AttributeDescription BooleanOperatorAttribute = Builder.Enumeration<BooleanOperator>(
            "BooleanOperator", nameof(BooleanOperator.And),
            "Which held considerations must allow the option, And for all of them, Or for at least one, and Not, which holds one, for that one vetoing it.");

        private static readonly AttributeDescription RankOperatorAttribute =
            // Stryker disable once Linq : the swap of this member for a LINQ call does not compile inside nameof
            Builder.Enumeration<RankOperator>("RankOperator", nameof(RankOperator.Max),
                "How the held considerations' ranks combine, Max for the highest, Min for the lowest, Sum for their total.");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Creates a group that holds nothing until a configuration loads.</summary>
        internal GroupConsideration()
        {
        }

        /// <summary>Creates a group that holds the given considerations.</summary>
        internal GroupConsideration(IEnumerable<ConsiderationBase> considerations) : base(considerations)
        {
        }

        /// <summary>
        ///     Which of the considerations this one holds must allow its option, loaded from the BooleanOperator attribute.
        ///     Defaults to And.
        /// </summary>
        public BooleanOperator BooleanOperator { get; internal set; } = BooleanOperator.And;

        /// <summary>
        ///     How the ranks of the considerations this one holds combine, loaded from the RankOperator attribute. Defaults
        ///     to Max.
        /// </summary>
        public RankOperator RankOperator { get; internal set; } = RankOperator.Max;

        /// <inheritdoc />
        protected override bool OnGroupInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.TryGetString(BooleanOperatorAttribute.Name, out string booleanOperatorText) &&
                !TryReadBooleanOperator(node, booleanOperatorText))
            {
                return false;
            }

            if (node.TryGetString(RankOperatorAttribute.Name, out string rankOperatorText) &&
                !TryReadRankOperator(node, rankOperatorText))
            {
                return false;
            }

            if (BooleanOperator == BooleanOperator.Not && Considerations.Count != 1)
            {
                ReportNotHoldingOtherThanOne(creationData);
                return false;
            }

            WarnWhenATuningFixesTheAnswer(creationData);
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Under And with Max this is the base's standard totals. Every other pairing walks the considerations by index,
        ///     so no enumerator is allocated.
        /// </remarks>
        protected override WeightValues CombineWeights()
        {
            if (BooleanOperator == BooleanOperator.And && RankOperator == RankOperator.Max)
            {
                return Totals;
            }

            IReadOnlyList<ConsiderationBase> held = Considerations;
            bool leavesOutVetoes = BooleanOperator == BooleanOperator.Or && AnyAllows(held);

            float addend = 0f;
            float multiplier = leavesOutVetoes ? 0f : 1f;
            float rank = float.MinValue;
            bool hasRank = false;

            for (int index = 0; index < held.Count; index++)
            {
                WeightValues weights = held[index].Weights;

                if (leavesOutVetoes && weights.Multiplier <= 0f)
                {
                    continue;
                }

                addend += weights.Addend;
                multiplier = leavesOutVetoes
                    ? MathF.Max(multiplier, weights.Multiplier)
                    : multiplier * weights.Multiplier;

                if (weights.Rank != float.MinValue)
                {
                    rank = hasRank ? CombineRanks(rank, weights.Rank) : weights.Rank;
                    hasRank = true;
                }
            }

            if (BooleanOperator == BooleanOperator.Not)
            {
                multiplier = multiplier > 0f ? 0f : 1f;
            }

            return new WeightValues(addend, multiplier, rank);
        }

        /// <summary>True when any of the considerations held reports a multiplier above 0 this cycle.</summary>
        private static bool AnyAllows(IReadOnlyList<ConsiderationBase> held)
        {
            for (int index = 0; index < held.Count; index++)
            {
                if (held[index].Weights.Multiplier > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Combines a rank already read with the next one, under RankOperator.</summary>
        private float CombineRanks(float combined, float rank)
        {
            switch (RankOperator)
            {
                case RankOperator.Min:
                    return MathF.Min(combined, rank);
                case RankOperator.Sum:
                    return combined + rank;
                default:
                    return MathF.Max(combined, rank);
            }
        }

        /// <summary>Reads the BooleanOperator attribute's text in any casing, reporting text that names no operator.</summary>
        private bool TryReadBooleanOperator(SpecificationNode node, string text)
        {
            if (string.Equals(text, nameof(BooleanOperator.And), StringComparison.OrdinalIgnoreCase))
            {
                BooleanOperator = BooleanOperator.And;
                return true;
            }

            if (string.Equals(text, nameof(BooleanOperator.Or), StringComparison.OrdinalIgnoreCase))
            {
                BooleanOperator = BooleanOperator.Or;
                return true;
            }

            if (string.Equals(text, nameof(BooleanOperator.Not), StringComparison.OrdinalIgnoreCase))
            {
                BooleanOperator = BooleanOperator.Not;
                return true;
            }

            node.ReportMalformed(BooleanOperatorAttribute.Name, text, "And, Or or Not");
            return false;
        }

        /// <summary>Reads the RankOperator attribute's text in any casing, reporting text that names no operator.</summary>
        private bool TryReadRankOperator(SpecificationNode node, string text)
        {
            // Stryker disable once Linq : the swap of this member for a LINQ call does not compile inside nameof
            if (string.Equals(text, nameof(RankOperator.Max), StringComparison.OrdinalIgnoreCase))
            {
                RankOperator = RankOperator.Max;
                return true;
            }

            // Stryker disable once Linq : the swap of this member for a LINQ call does not compile inside nameof
            if (string.Equals(text, nameof(RankOperator.Min), StringComparison.OrdinalIgnoreCase))
            {
                RankOperator = RankOperator.Min;
                return true;
            }

            // Stryker disable once Linq : the swap of this member for a LINQ call does not compile inside nameof
            if (string.Equals(text, nameof(RankOperator.Sum), StringComparison.OrdinalIgnoreCase))
            {
                RankOperator = RankOperator.Sum;
                return true;
            }

            node.ReportMalformed(RankOperatorAttribute.Name, text, "Max, Min or Sum");
            return false;
        }

        /// <summary>Reports a Not group that holds no consideration or more than one.</summary>
        private void ReportNotHoldingOtherThanOne(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            creationData.ReportError(
                $"Line {node.LineNumber}, the {node.NameSpelling} element's BooleanOperator is Not and it holds {Considerations.Count} considerations, where a Not group holds exactly one consideration and several go inside an Or or And group");
        }

        /// <summary>Warns when a Tuning held under Or or Not fixes the group's answer on every cycle.</summary>
        /// <remarks>
        ///     Under Or a Tuning that vetoes is left out, so it fixes nothing. Under Not the one Tuning fixes the answer
        ///     either way, and the group does the opposite of what the Tuning does.
        /// </remarks>
        private void WarnWhenATuningFixesTheAnswer(AiCreationData creationData)
        {
            if (BooleanOperator == BooleanOperator.And)
            {
                return;
            }

            IReadOnlyList<ConsiderationBase> held = Considerations;

            for (int index = 0; index < held.Count; index++)
            {
                // Stryker disable once all : a mutant of a pattern whose variable is used after the if does not compile
                if (held[index] is not TuningConsideration tuning)
                {
                    continue;
                }

                bool allows = tuning.Weights.Multiplier > 0f;

                if (!allows && BooleanOperator == BooleanOperator.Or)
                {
                    continue;
                }

                SpecificationNode node = creationData.Node;
                string does = allows ? "allows" : "vetoes";
                string outcome = allows && BooleanOperator == BooleanOperator.Not ? "vetoes" : "allows";

                creationData.ReportWarning(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element's BooleanOperator is {BooleanOperator} and it holds a Tuning that {does}, so it {outcome} the option on every cycle");
                return;
            }
        }
    }
}
