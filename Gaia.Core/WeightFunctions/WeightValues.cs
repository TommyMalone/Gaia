namespace Gaia.Core.WeightFunctions
{
    public readonly struct WeightValues
    {
        public float Addend { get; }
        public float Multiplier { get; }
        public float Rank { get; }

        /// <summary>Creates weight values from an addend, a multiplier and a rank.</summary>
        public WeightValues(float addend, float multiplier, float rank)
        {
            Addend = addend;
            Multiplier = multiplier;
            Rank = rank;
        }

        public static readonly WeightValues Neutral = new(0f, 1, float.MinValue);
        public static readonly WeightValues Vetoed = new(0f, 0f, float.MinValue);
    }
}
