using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Services.Random;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Reasoners
{
    /// <summary>
    ///     A reasoner that narrows its options to the highest rank, screens out the ones far below the best weight, and
    ///     picks among the rest at random in proportion to weight.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.6.2</seealso>
    /// <seealso>Kevin Dill, "Dual-Utility Reasoning", Game AI Pro 2, CRC Press, 2015</seealso>
    public class DualUtilityReasoner : ReasonerBase
    {
        private static readonly AiString WeightScreeningFactorAttribute = "WeightScreeningFactor";

        /// <summary>Where the reasoner draws the roll that picks among the surviving options.</summary>
        public IRandomSource RandomSource { get; set; } = new Xoshiro128RandomSource();

        /// <summary>
        ///     How close to the best weight an option must come to remain a candidate for random-weighted selection, from 0
        ///     for no screening to 1 for only the best. The best weight within the winning rank is multiplied by this factor, and
        ///     options whose weight falls below the result are screened out before the random pick.
        /// </summary>
        public float WeightScreeningFactor { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            RandomSource = creationData.RandomSource;

            if (node.HasAttribute(WeightScreeningFactorAttribute))
            {
                if (!node.ReadFloat(WeightScreeningFactorAttribute, out float weightScreeningFactor))
                {
                    return false;
                }

                WeightScreeningFactor = weightScreeningFactor;
            }

            return true;
        }

        /// <inheritdoc />
        protected override void Think()
        {
            // 1. Eliminate the options with a weight of zero.
            // 2. Find the highest rank category, and eliminate options that don't belong to it.
            float highestRank = float.MinValue;
            foreach (OptionBase option in OptionList)
            {
                if (option.IsValid && option.Rank > highestRank)
                {
                    highestRank = option.Rank;
                }
            }

            float highestWeightInTopRank = 0f;
            foreach (OptionBase option in OptionList)
            {
                // Any option with a Rank below ScreeningRank returns a Weight of 0 from here on, so the highest
                // weight found in this pass is the highest weight in the top rank category.
                option.ScreeningRank = highestRank;

                if (option.Weight > highestWeightInTopRank)
                {
                    highestWeightInTopRank = option.Weight;
                }
            }

            // 3. Eliminate options much worse than the best remaining one. A factor of 0 skips this step.
            float screeningWeight = highestWeightInTopRank * WeightScreeningFactor;
            float totalWeightPostScreening = 0f;

            foreach (OptionBase option in OptionList)
            {
                option.ScreeningWeight = screeningWeight;
                // option.Weight returns 0 if the option's Weight is below its ScreeningWeight.
                totalWeightPostScreening += option.Weight;
            }

            // 4. Use weight-based random to select from the options that remain.
            SelectOption(GetWeightedRandomOption(totalWeightPostScreening));
        }

        /// <summary>Choose an option at random, in proportion to weight.</summary>
        /// <param name="totalWeightPostScreening">The sum of the options' weights, after screening.</param>
        /// <returns>The selected option, or null if no option has any weight.</returns>
        private OptionBase? GetWeightedRandomOption(float totalWeightPostScreening)
        {
            if (totalWeightPostScreening <= 0f)
            {
                return null;
            }

            float normalizedRoll = RandomSource.NextFloat();
            LastDecisionRoll = normalizedRoll;

            float roll = normalizedRoll * totalWeightPostScreening;
            OptionBase? selected = null;

            foreach (OptionBase option in OptionList)
            {
                if (option.Weight <= 0f)
                {
                    continue;
                }

                selected = option;
                roll -= option.Weight;

                if (roll < 0f)
                {
                    break;
                }
            }

            return selected;
        }
    }
}
