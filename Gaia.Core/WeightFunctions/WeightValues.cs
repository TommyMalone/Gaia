using System;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>What a consideration reports about the option it belongs to.</summary>
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
        ///     meaning, two of them would cancel to a positive weight, so TryRead refuses one.
        /// </remarks>
        public float Multiplier { get; }

        /// <summary>The category this consideration puts its option in.</summary>
        /// <remarks>
        ///     An option takes the highest rank any of its considerations reported. A reasoner then keeps only the options
        ///     sharing the highest rank it found across all of them and decides between those on weight alone, so rank settles
        ///     which options are eligible rather than scoring them. An option of higher rank beats every option beneath it however
        ///     little it weighs. The identity is float.MinValue.
        /// </remarks>
        public float Rank { get; }

        /// <summary>Creates weight values from an addend, a multiplier and a rank.</summary>
        public WeightValues(float addend, float multiplier, float rank)
        {
            Addend = addend;
            Multiplier = multiplier;
            Rank = rank;
        }

        /// <summary>Contributes nothing, being the identity value for all three.</summary>
        public static readonly WeightValues Neutral = new(0f, 1f, float.MinValue);

        /// <summary>Vetoes the option, whatever the rest of its considerations report.</summary>
        public static readonly WeightValues Vetoed = new(0f, 0f, float.MinValue);

        private static readonly AiString AddendAttribute = "Addend";
        private static readonly AiString MultiplierAttribute = "Multiplier";
        private static readonly AiString RankAttribute = "Rank";

        /// <summary>Reads the optional Addend, Multiplier and Rank attributes of an element as one set of weight values.</summary>
        /// <remarks>
        ///     An absent attribute keeps its identity value, so an element saying nothing reads as <see cref="Neutral" /> and
        ///     a veto is written Multiplier="0". Malformed text fails the read once the node has reported it, and so does a
        ///     multiplier below 0, which parses but has no meaning a configuration could intend.
        /// </remarks>
        internal static bool TryRead(SpecificationNode node, out WeightValues values)
        {
            values = Neutral;

            float addend = 0f;
            float multiplier = 1f;
            float rank = float.MinValue;

            if (node.HasAttribute(AddendAttribute) && !node.ReadFloat(AddendAttribute, out addend))
            {
                return false;
            }

            if (node.HasAttribute(MultiplierAttribute))
            {
                if (!node.ReadFloat(MultiplierAttribute, out multiplier))
                {
                    return false;
                }

                if (multiplier < 0f)
                {
                    node.TryGetString(MultiplierAttribute, out string multiplierText);
                    node.ReportMalformed(MultiplierAttribute, multiplierText, "0 or above");
                    return false;
                }
            }

            if (node.HasAttribute(RankAttribute) && !node.ReadFloat(RankAttribute, out rank))
            {
                return false;
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
            return $"Addend {Addend}, Multiplier {Multiplier}, Rank {Rank}";
        }
    }
}
