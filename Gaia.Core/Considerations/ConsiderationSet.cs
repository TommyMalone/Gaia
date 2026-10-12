using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Specifications;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>The set of considerations belonging to one option.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.4, Listing 8.10</seealso>
    public sealed class ConsiderationSet
    {
        private readonly List<ConsiderationBase> _considerations = new();

        /// <summary>The option the set belongs to, once AttachTo has said which that is.</summary>
        private OptionBase? _option;

        /// <summary>Creates an empty set for its option and loading to fill.</summary>
        internal ConsiderationSet()
        {
        }

        /// <summary>The sum of the considerations' addends, as of the last Calculate.</summary>
        public float Addend { get; private set; }

        /// <summary>The product of the considerations' multipliers, as of the last Calculate.</summary>
        public float Multiplier { get; private set; }

        /// <summary>The addends' sum scaled by the multipliers' product, as of the last Calculate.</summary>
        public float Weight => Addend * Multiplier;

        /// <summary>The highest rank any consideration in the set reported.</summary>
        public float Rank { get; private set; } = float.MinValue;

        /// <summary>True if this set holds a <see cref="TuningConsideration" />, directly or inside a group at any depth.</summary>
        /// <remarks>
        ///     Set as each consideration is added. A group is filled when it is made or loaded, before any set takes it, so
        ///     Add sees everything the group holds.
        /// </remarks>
        internal bool HasTuning { get; private set; }

        /// <summary>The considerations in the set, in the order they were added.</summary>
        /// <remarks>
        ///     Internal, and read by the component that holds the set. <see cref="OptionBase" /> reads it for the load-time
        ///     lint that judges whether an option's fixed values can ever select it, <see cref="OptionBase.Considerations" /> and
        ///     <see cref="GroupConsiderationBase.Considerations" /> hand it to readers outside the library, and a group's
        ///     CombineWeights may read it every cycle. A foreach over it boxes an enumerator and an indexed for does not, so a
        ///     reader on the cycle path loops by index.
        /// </remarks>
        internal IReadOnlyList<ConsiderationBase> Considerations => _considerations;

        /// <summary>Loads one consideration into the set for each Consideration child the node holds.</summary>
        /// <returns>True when every child loaded. False after reporting what went wrong.</returns>
        internal bool Init(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            // Every child is a Consideration, since the factory refused anything else against the description.
            foreach (SpecificationNode child in node.Children)
            {
                if (!creationData.Factories.Considerations.TryCreate(creationData.ForChild(child),
                        out ConsiderationBase? consideration))
                {
                    return false;
                }

                Add(consideration!);
            }

            return true;
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
                rank = MathF.Max(rank, weights.Rank);
            }

            Addend = addend;
            Multiplier = multiplier;
            Rank = rank;
        }

        /// <summary>Calls AttachTo on every consideration in the set with the option the set belongs to.</summary>
        internal void AttachTo(OptionBase option)
        {
            _option = option;

            foreach (ConsiderationBase consideration in _considerations)
            {
                consideration.AttachTo(option);
            }
        }

        /// <summary>Adds a consideration to the set, attaching it to the option if the set already has one.</summary>
        internal void Add(ConsiderationBase consideration)
        {
            _considerations.Add(consideration);

            if (_option != null)
            {
                consideration.AttachTo(_option);
            }

            if (consideration is TuningConsideration or GroupConsiderationBase { HasTuning: true })
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
