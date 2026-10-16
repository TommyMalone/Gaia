using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     What a <see cref="Considerations.ConsiderationBase" /> reports about the <see cref="Options.OptionBase" /> it
    ///     belongs to.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.6.2.1</seealso>
    public readonly struct WeightValues : IEquatable<WeightValues>
    {
        /// <summary>How much this consideration wants its option chosen.</summary>
        /// <remarks>
        ///     Summed with the option's other addends, and the total is then scaled by the total multiplier to give the
        ///     option's weight. The identity is 0.
        /// </remarks>
        public float Addend { get; }

        /// <summary>How much of what the addends asked for this consideration will allow.</summary>
        /// <remarks>
        ///     Multiplied with the option's other multipliers. A value of 0 vetoes the option outright however large its
        ///     addends are, and a value in between weakens it without vetoing it. The identity is 1. A value below 0 has no
        ///     meaning, since two of them would multiply to a positive weight, so TryRead refuses one.
        /// </remarks>
        public float Multiplier { get; }

        /// <summary>The category this consideration puts its option in.</summary>
        /// <remarks>
        ///     An option takes the highest rank any of its considerations reported. A
        ///     <see cref="Reasoners.DualUtilityReasoner" /> then keeps only the options sharing the highest rank it found among
        ///     those whose weight is above 0 and decides between those on weight alone, so rank settles which options are eligible
        ///     rather than scoring them. A <see cref="Reasoners.RuleBasedReasoner" /> or <see cref="Reasoners.SequenceReasoner" />
        ///     never reads rank. An option of higher rank beats every option beneath it however little it weighs. The identity is
        ///     <see cref="NoRank" />.
        /// </remarks>
        public float Rank { get; }

        /// <summary>Creates weight values from an addend, a multiplier and a rank.</summary>
        public WeightValues(float addend, float multiplier, float rank)
        {
            Addend = addend;
            Multiplier = multiplier;
            Rank = rank;
        }

        /// <summary>
        ///     The rank of a consideration or an option that has none. It is float.MinValue, beneath every rank a designer or
        ///     a game would write.
        /// </summary>
        public const float NoRank = float.MinValue;

        /// <summary>Contributes nothing, being the identity value for all three.</summary>
        public static readonly WeightValues Neutral = new(0f, 1f, NoRank);

        /// <summary>Vetoes the option, whatever the rest of its considerations report.</summary>
        public static readonly WeightValues Vetoed = new(0f, 0f, NoRank);

        private static readonly AttributeDescription AddendAttribute = AttributeDescription.Optional("Addend", "Addend",
            AttributeKind.Float, Number(Neutral.Addend),
            "How much the option is wanted. Summed with the option's other addends.");

        private static readonly AttributeDescription MultiplierAttribute = AttributeDescription.Optional("Multiplier",
            "Multiplier", AttributeKind.Float, Number(Neutral.Multiplier),
            "How much of what the addends asked for is allowed, 0 or more. Multiplied with the option's other multipliers.");

        private static readonly AttributeDescription RankAttribute = AttributeDescription.Optional("Rank", "Rank",
            AttributeKind.Float, null,
            "The tier the option is put in, where a higher tier beats every lower one. Absent, the lowest tier.");

        private static readonly AttributeDescription VetoAttribute = AttributeDescription.Optional("Veto", "Veto",
            AttributeKind.Bool, "false", "True to rule the option out whatever else is reported.");

        /// <summary>
        ///     Describes the four attributes TryRead reads over <see cref="Neutral" />, for a description whose element reads
        ///     them.
        /// </summary>
        /// <remarks>These are the attributes TryRead reads by, so an element reads exactly what this list names.</remarks>
        internal static readonly IReadOnlyList<AttributeDescription> Attributes = new[]
            { AddendAttribute, MultiplierAttribute, RankAttribute, VetoAttribute };

        /// <summary>
        ///     Describes an element that is one set of weight values and nothing else, such as True, False, Missing or
        ///     Default.
        /// </summary>
        internal static readonly ComponentDescription Description = new("One set of weight values.", Attributes,
            Array.Empty<SlotDescription>());

        /// <summary>
        ///     Describes the four attributes TryRead reads over the given defaults, for an element whose identity is not
        ///     Neutral.
        /// </summary>
        /// <remarks>A rank of <see cref="NoRank" /> is no text a designer would write, so it is described as having no default.</remarks>
        internal static IReadOnlyList<AttributeDescription> DescribeAttributes(WeightValues defaults)
        {
            string? rankText = defaults.Rank == NoRank ? null : Number(defaults.Rank);

            return new[]
            {
                AddendAttribute.WithDefault(Number(defaults.Addend)),
                MultiplierAttribute.WithDefault(Number(defaults.Multiplier)), RankAttribute.WithDefault(rankText),
                VetoAttribute,
            };
        }

        /// <summary>Prints a default invariantly, so a decimal mark is always a point.</summary>
        private static string Number(float value)
        {
            return FormattableString.Invariant($"{value}");
        }

        /// <summary>Reads the optional Addend, Multiplier, Rank and Veto attributes of an element as one set of weight values.</summary>
        /// <remarks>
        ///     An absent attribute keeps its identity value, so an element saying nothing reads as <see cref="Neutral" />. A
        ///     veto is written Veto="true", in any casing, and a Veto beside a Multiplier is refused whatever either says, since
        ///     both name the multiplier.
        /// </remarks>
        /// <seealso>"Modular AI", Section 8.6.2.1, Listing 8.9</seealso>
        internal static bool TryRead(SpecificationNode node, out WeightValues values)
        {
            return TryRead(node, Neutral, out values);
        }

        /// <summary>Reads the same attributes over the given defaults, for an element whose identity is not Neutral.</summary>
        /// <remarks>
        ///     <see cref="Considerations.TuningConsideration" /> is the one element that has a unique default value, an
        ///     addend of 1. Everything else reads over Neutral.
        /// </remarks>
        internal static bool TryRead(SpecificationNode node, WeightValues defaults, out WeightValues values)
        {
            values = defaults;

            float addend = defaults.Addend;
            float multiplier = defaults.Multiplier;
            float rank = defaults.Rank;

            if (node.HasAttribute(AddendAttribute.Name) && !node.ReadFloat(AddendAttribute.Name, out addend))
            {
                return false;
            }

            if (node.HasAttribute(MultiplierAttribute.Name))
            {
                if (!node.ReadFloat(MultiplierAttribute.Name, out multiplier))
                {
                    return false;
                }

                if (multiplier < 0f)
                {
                    node.TryGetString(MultiplierAttribute.Name, out string multiplierText);
                    node.ReportMalformed(MultiplierAttribute.Name, multiplierText, "0 or above");
                    return false;
                }
            }

            if (node.HasAttribute(RankAttribute.Name) && !node.ReadFloat(RankAttribute.Name, out rank))
            {
                return false;
            }

            if (node.HasAttribute(VetoAttribute.Name))
            {
                if (!node.ReadBool(VetoAttribute.Name, out bool veto))
                {
                    return false;
                }

                if (node.HasAttribute(MultiplierAttribute.Name))
                {
                    node.ReportConflict(VetoAttribute.Name, MultiplierAttribute.Name);
                    return false;
                }

                if (veto)
                {
                    multiplier = 0f;
                }
            }

            values = new WeightValues(addend, multiplier, rank);
            return true;
        }

        public bool Equals(WeightValues other)
        {
            return Addend.Equals(other.Addend) && Multiplier.Equals(other.Multiplier) && Rank.Equals(other.Rank);
        }

        public override bool Equals(object? obj)
        {
            return obj is WeightValues other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Addend, Multiplier, Rank);
        }

        public static bool operator ==(WeightValues left, WeightValues right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeightValues left, WeightValues right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"Addend {Addend}, Multiplier {Multiplier}, Rank {Rank}");
        }
    }
}
