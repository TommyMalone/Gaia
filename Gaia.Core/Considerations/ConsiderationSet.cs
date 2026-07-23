using System;
using System.Collections.Generic;
using Gaia.Core.Temp;

namespace Gaia.Core.Considerations
{
    public sealed class ConsiderationSet
    {
        /// <summary>The considerations in the set, in the order they were added.</summary>
        private readonly List<ConsiderationBase> _considerations = new();

        public float Addend { get; private set; }
        public float Multiplier { get; private set; }

        public float UnscreenedWeight => Addend * Multiplier;
        public float UnscreenedRank { get; private set; } = float.MinValue;

        public float ScreeningWeight { get; set; }
        public float ScreeningRank { get; set; } = float.MinValue;

        public float Weight => PassesScreening ? UnscreenedWeight : 0f;
        public float Rank => UnscreenedRank;

        private bool PassesScreening => UnscreenedRank >= ScreeningRank && UnscreenedWeight >= ScreeningWeight;

        public bool IsSelected { get; private set; }

        /// <summary>True if this set already contains a Tuning consideration.</summary>
        public bool HasTuning { get; private set; }

        public bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        public void Calculate()
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
            UnscreenedRank = rank;

            ScreeningWeight = 0f;
            ScreeningRank = float.MinValue;
        }

        public void Add(ConsiderationBase consideration)
        {
            _considerations.Add(consideration);
            if (consideration is TuningConsideration)
            {
                // If considerations are ever allowed to be removed, a more robust method will be needed to tell
                // whether a TuningConsideration exists.
                HasTuning = true;
            }
        }

        public void Select()
        {
            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.Select();
            }

            IsSelected = true;
        }

        public void Deselect()
        {
            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.Deselect();
            }

            IsSelected = false;
        }
    }
}
