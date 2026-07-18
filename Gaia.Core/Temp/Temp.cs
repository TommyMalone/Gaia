namespace Gaia.Core.Temp
{
    // Definitions in this file are either stubs to suppress compilation error, or definitions that will be moved to a
    // more permenant home.

    /// <summary>Placeholder. Will carry the data needed to construct an AI component.</summary>
    public sealed class AiCreationData
    {
    }

    public readonly struct WeightValues
    {
        public float Addend { get; }
        public float Multiplier { get; }
        public float Rank { get; }

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
