using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>The set of considerations belonging to one option.</summary>
    /// <seealso>"Modular AI", Section 8.6.2.4, Listing 8.10</seealso>
    public sealed class ConsiderationSet
    {
        private static readonly AiString ConsiderationElement = "Consideration";

        /// <summary>The considerations in the set, in the order they were added.</summary>
        private readonly List<ConsiderationBase> _considerations = new();

        /// <summary>The option the set belongs to, once AttachTo has said which that is.</summary>
        private OptionBase? _option;

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

        /// <summary>The considerations in the set, in the order they were added.</summary>
        /// <remarks>
        ///     Internal, and opened for the load-time lint on <see cref="OptionBase" /> that judges whether an option's fixed
        ///     values can ever select it. Nothing on the cycle path reads it, so the enumerator an IReadOnlyList boxes is paid
        ///     once per option at load rather than every cycle.
        /// </remarks>
        internal IReadOnlyList<ConsiderationBase> Considerations => _considerations;

        /// <summary>Loads one consideration into the set for each Consideration child the node holds.</summary>
        /// <returns>True when every child loaded. False after reporting what went wrong.</returns>
        internal bool Init(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != ConsiderationElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

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
                rank = Math.Max(rank, weights.Rank);
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
