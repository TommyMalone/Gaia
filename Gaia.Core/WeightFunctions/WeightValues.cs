namespace Gaia.Core.WeightFunctions
{
    /// <summary>What a consideration reports about the option it belongs to.</summary>
    public readonly struct WeightValues
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
        ///     addends are, and a value in between weakens it without vetoing it. The identity is 1.
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
    }
}
