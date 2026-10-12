using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that holds considerations of its own and reports their combined values as one.</summary>
    /// <remarks>
    ///     <para>
    ///         The base loads the considerations a group holds, passes its option and its selection on to them, and
    ///         calculates each of them every cycle before it asks CombineWeights how their values become the group's. A
    ///         derived group answers that one question, and may read attributes of its own in OnGroupInit. The hooks the base
    ///         forwards are sealed, so no derived group can leave one out.
    ///     </para>
    ///     <para>
    ///         Every child element is loaded as a consideration, so a derived group's description adds ConsiderationSlot and
    ///         no other slot. A Tuning consideration inside any group counts as its option's, so the option is given no
    ///         default beside it.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.4</seealso>
    public abstract class GroupConsiderationBase : ConsiderationBase
    {
        /// <summary>Describes the Consideration children a group holds, for a derived group's description to add.</summary>
        /// <remarks>
        ///     Public so that a group a game writes adds the piece its base reads rather than restating the element name in a
        ///     description of its own.
        /// </remarks>
        public static readonly SlotDescription ConsiderationSlot = new("Consideration", "Consideration",
            ConceptualAbstraction.Considerations, 0, SlotDescription.Unbounded);

        private readonly ConsiderationSet _set = new();

        /// <summary>Creates a group that holds nothing until a configuration loads.</summary>
        protected GroupConsiderationBase()
        {
        }

        /// <summary>Creates a group that holds the given considerations.</summary>
        protected GroupConsiderationBase(IEnumerable<ConsiderationBase> considerations)
        {
            foreach (ConsiderationBase consideration in considerations)
            {
                _set.Add(consideration);
            }
        }

        /// <summary>The considerations this group holds, in the order they were added.</summary>
        /// <remarks>
        ///     A rule in CombineWeights may read them every cycle. A foreach over this list allocates a boxed enumerator each
        ///     time it runs and an indexed for does not, so a group or a host that reads it every cycle loops by index.
        /// </remarks>
        public IReadOnlyList<ConsiderationBase> Considerations => _set.Considerations;

        /// <summary>True if a <see cref="TuningConsideration" /> is among the considerations this group holds, at any depth.</summary>
        internal bool HasTuning => _set.HasTuning;

        /// <summary>
        ///     What the considerations this group holds reported this cycle under the library's standard rule, their addends
        ///     summed, their multipliers multiplied and the highest of their ranks.
        /// </summary>
        /// <remarks>
        ///     Calculate runs the group's set, which sums, multiplies and takes the highest as it walks the held
        ///     considerations, so the three stand ready before CombineWeights is asked, and a rule that starts from the standard
        ///     rule reads them here. The base itself never reads them, and the shipped group's whole rule is to report them.
        /// </remarks>
        protected WeightValues Totals => new(_set.Addend, _set.Multiplier, _set.Rank);

        /// <inheritdoc />
        protected sealed override bool OnInit(AiCreationData creationData)
        {
            if (!_set.Init(creationData))
            {
                return false;
            }

            return OnGroupInit(creationData);
        }

        /// <summary>Loads the derived group's own configuration, once the considerations it holds are in.</summary>
        /// <remarks>The base reports true, so a group with nothing of its own to load leaves this alone.</remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnGroupInit(AiCreationData creationData)
        {
            return true;
        }

        /// <inheritdoc />
        protected sealed override void OnAttach(OptionBase option)
        {
            _set.AttachTo(option);
        }

        /// <inheritdoc />
        public sealed override void Calculate()
        {
            _set.Calculate();
            Weights = CombineWeights();
        }

        /// <inheritdoc />
        protected sealed override void OnSelect()
        {
            _set.Select();
        }

        /// <inheritdoc />
        protected sealed override void OnDeselect()
        {
            _set.Deselect();
        }

        /// <summary>The group's values this cycle, from what the considerations it holds reported.</summary>
        /// <remarks>
        ///     Each consideration the group holds has already calculated this cycle when this is asked, so Considerations and
        ///     Totals read this cycle's values.
        /// </remarks>
        protected abstract WeightValues CombineWeights();
    }
}
