using System;
using System.Collections.Generic;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>The set of considerations belonging to one option.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.4, Listing 8.10</seealso>
    public sealed class ConsiderationSet
    {
        /// <summary>The considerations in the set, in the order they were added.</summary>
        private readonly List<ConsiderationBase> _considerations = new();

        /// <summary>The sum of the considerations' addends, as of the last Calculate.</summary>
        public float Addend { get; private set; }

        /// <summary>The product of the considerations' multipliers, as of the last Calculate.</summary>
        public float Multiplier { get; private set; }

        /// <summary>The addends' sum scaled by the multipliers' product, as of the last Calculate.</summary>
        public float Weight => Addend * Multiplier;

        /// <summary>The highest rank any consideration in the set reported.</summary>
        public float Rank { get; private set; } = float.MinValue;

        /// <summary>True if this set already contains a <see cref="TuningConsideration" />.</summary>
        internal bool HasTuning { get; private set; }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        internal bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Evaluates every consideration and combines the results.</summary>
        internal void Calculate()
        {
            float addend = 0f;
            float multiplier = 1f;
            float rank = float.MinValue;

            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.Calculate();

                WeightValues weights = consideration.Weights;
                addend += weights.Addend;
                multiplier *= weights.Multiplier;
                rank = Math.Max(rank, weights.Rank);
            }

            Addend = addend;
            Multiplier = multiplier;
            Rank = rank;
        }

        /// <summary>Adds a consideration to the set.</summary>
        internal void Add(ConsiderationBase consideration)
        {
            _considerations.Add(consideration);

            if (consideration is TuningConsideration)
            {
                // If considerations are ever allowed to be removed, a more robust method will be needed to tell
                // whether a TuningConsideration exists.
                HasTuning = true;
            }
        }

        /// <summary>Calls Select on every consideration in the set.</summary>
        internal void Select()
        {
            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.Select();
            }
        }

        /// <summary>Calls Deselect on every consideration in the set.</summary>
        internal void Deselect()
        {
            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.Deselect();
            }
        }
    }
}
