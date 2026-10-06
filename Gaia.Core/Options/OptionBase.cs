using System.Collections.Generic;
using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Log;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Options
{
    /// <summary>
    ///     One thing an AI could choose to do, pairing the considerations that score it with the actions that run if it
    ///     is chosen.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.3.1, Listing 8.11</seealso>
    public abstract class OptionBase : IAiComponent
    {
        /// <summary>Describes the Name attribute Init reads by on every option, for a derived option's description to add.</summary>
        /// <remarks>
        ///     Public so that an option a game writes adds the piece its base reads rather than restating the spelling in a
        ///     description of its own.
        /// </remarks>
        public static readonly AttributeDescription NameAttribute = AttributeDescription.Required("Name", "Name",
            AttributeKind.String, "What the option is called in decisions and the log.");

        private readonly List<ActionBase> _actions = new();
        private readonly ConsiderationSet _considerations = new();

        /// <summary>
        ///     Attaches the consideration set to this option. The set passes the option along to each consideration as it is
        ///     added, so a consideration that needs to look at its own option can.
        /// </summary>
        protected OptionBase()
        {
            _considerations.AttachTo(this);
        }

        /// <summary>What this option is called, which is how a reasoner names it when it reports a decision.</summary>
        public AiString Name { get; protected internal set; } = AiString.Empty;

        /// <summary>True while this option is the one its reasoner has selected.</summary>
        public bool IsSelected { get; private set; }

        /// <summary>
        ///     The weight the reasoner acts on, which is what the considerations added up to, or 0 if the option fell short
        ///     of either ScreeningRank or ScreeningWeight.
        /// </summary>
        public float Weight => PassesScreening ? _considerations.Weight : 0f;

        /// <summary>The highest rank the considerations reported.</summary>
        public float Rank => _considerations.Rank;

        /// <summary>
        ///     True if this option can be selected, which is whenever its Weight is above 0. An option that can be selected
        ///     this cycle is eligible, or a candidate, and a vetoed option is neither.
        /// </summary>
        public bool IsValid => Weight > 0f;

        /// <summary>
        ///     The weight this option must reach to survive, set by <see cref="DualUtilityReasoner" /> from the best weight
        ///     it found among the options at the highest rank, scaled by the reasoner's
        ///     <see cref="DualUtilityReasoner.WeightScreeningFactor" />.
        /// </summary>
        public float ScreeningWeight { get; internal set; }

        /// <summary>
        ///     The rank this option must reach to survive, set by the reasoner from the best rank it found across its valid
        ///     options.
        /// </summary>
        public float ScreeningRank { get; internal set; } = float.MinValue;

        /// <summary>True unless the reasoner's comparison against the other options screened this option out.</summary>
        /// <remarks>
        ///     An option below either ScreeningRank or ScreeningWeight reports a Weight of 0 rather than being taken out of a
        ///     list, which is how a reasoner narrows its options without building a subset. <see cref="DualUtilityReasoner" /> is
        ///     the only reasoner that sets these thresholds. For the other reasoner types PassesScreening is always true.
        /// </remarks>
        internal bool PassesScreening =>
            _considerations.Rank >= ScreeningRank && _considerations.Weight >= ScreeningWeight;

        /// <summary>
        ///     The considerations' combined weight before screening, which is what a decision record reports so a screened
        ///     option still shows what it weighed.
        /// </summary>
        internal float WeightBeforeScreening => _considerations.Weight;

        /// <summary>
        ///     The considerations that score this option, in the order they were added, including the default
        ///     TuningConsideration loading adds to an option that writes none.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <see cref="ConsiderationSet" /> that holds them is an implementation detail of the option, so this is
        ///         where a reader reaches them. An option whose element writes no Tuning consideration gains the default one at
        ///         load, which has no element, so its list holds one more entry than the element has Consideration children. An
        ///         option assembled in code gains the default when its reasoner is enabled.
        ///     </para>
        ///     <para>
        ///         A foreach over this list allocates a boxed enumerator each time it runs and an indexed for does not, so a
        ///         host that reads it every tick loops by index.
        ///     </para>
        /// </remarks>
        public IReadOnlyList<ConsiderationBase> Considerations => _considerations.Considerations;

        /// <summary>The actions that run while this option is selected, in the order they were added.</summary>
        /// <remarks>
        ///     A foreach over this list allocates a boxed enumerator each time it runs and an indexed for does not, so a host
        ///     that reads it every tick loops by index.
        /// </remarks>
        public IReadOnlyList<ActionBase> Actions => _actions;

        /// <summary>True once every action on this option reports that it is done.</summary>
        public bool IsDone
        {
            get
            {
                foreach (ActionBase action in _actions)
                {
                    if (!action.IsDone)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Loads this option's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!creationData.Node.ReadString(NameAttribute.Name, out string name))
            {
                return false;
            }

            Name = name;

            if (!OnInit(creationData))
            {
                return false;
            }

            EnsureTuningConsideration();
            WarnWhenFixedValuesCannotSelect(creationData);
            return true;
        }

        /// <summary>Loads the derived option's own configuration.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected abstract bool OnInit(AiCreationData creationData);

        /// <summary>Adds a consideration to the set that scores this option.</summary>
        public void AddConsideration(ConsiderationBase consideration)
        {
            _considerations.Add(consideration);
        }

        /// <summary>Adds an action that runs while this option is selected.</summary>
        public void AddAction(ActionBase action)
        {
            _actions.Add(action);
        }

        /// <summary>Adds a default <see cref="TuningConsideration" /> if the option holds none.</summary>
        /// <remarks>
        ///     <para>
        ///         The default is <see cref="TuningConsideration.Default" />, an addend of 1 and a multiplier of 1. An option
        ///         whose other considerations only multiply would otherwise sum its addends to 0, leaving a weight of 0, and could
        ///         never be selected.
        ///     </para>
        ///     <para>
        ///         A loaded option gets this from Init once its configuration is complete. An option assembled in code never
        ///         runs Init, so <see cref="ReasonerBase.Enable" /> calls this on every option the reasoner holds, by which point
        ///         the assembler has added the considerations it means the option to have.
        ///     </para>
        /// </remarks>
        internal void EnsureTuningConsideration()
        {
            if (_considerations.HasTuning)
            {
                return;
            }

            AddConsideration(new TuningConsideration());
        }

        /// <summary>Warns when every consideration is a fixed tuning whose combined weight can never select the option.</summary>
        /// <remarks>
        ///     Only fixed values can be judged at load, since one dynamic consideration is enough to make the answer depend
        ///     on the world. A fixed weight of 0 or less fails <see cref="IsValid" /> every cycle, so under a reasoner that
        ///     selects by weight the option is configuration that can never run, which is sometimes a deliberate off switch and
        ///     accepts the one warning.
        /// </remarks>
        private void WarnWhenFixedValuesCannotSelect(AiCreationData creationData)
        {
            float addendSum = 0f;
            float multiplierProduct = 1f;

            foreach (ConsiderationBase consideration in _considerations.Considerations)
            {
                if (consideration is not TuningConsideration tuning)
                {
                    return;
                }

                addendSum += tuning.Weights.Addend;
                multiplierProduct *= tuning.Weights.Multiplier;
            }

            if (addendSum * multiplierProduct <= 0f)
            {
                SpecificationNode node = creationData.Node;

                creationData.LogSink.LogWarning(creationData.LogChannel,
                    $"Line {node.LineNumber}, the {node.NameSpelling} element's fixed values sum to a weight of 0 or less, a reasoner that selects by weight can never select it");
            }
        }

        /// <summary>Recalculates what the considerations add up to.</summary>
        /// <remarks>
        ///     Also clears ScreeningWeight and ScreeningRank. They belong to the cycle that set them and Calculate should
        ///     only be called once per cycle.
        /// </remarks>
        public void Calculate()
        {
            _considerations.Calculate();

            ScreeningWeight = 0f;
            ScreeningRank = float.MinValue;
        }

        /// <summary>Selects this option, which selects its considerations and actions. Does nothing if IsSelected.</summary>
        public void Select()
        {
            if (IsSelected)
            {
                return;
            }

            IsSelected = true;
            _considerations.Select();

            foreach (ActionBase action in _actions)
            {
                action.Select();
            }
        }

        /// <summary>Deselects this option, which deselects its considerations and actions. Does nothing unless IsSelected.</summary>
        public void Deselect()
        {
            if (!IsSelected)
            {
                return;
            }

            IsSelected = false;
            _considerations.Deselect();

            foreach (ActionBase action in _actions)
            {
                action.Deselect();
            }
        }

        /// <summary>Calls Update on every action, once per cycle while this option is selected.</summary>
        public void Update()
        {
            foreach (ActionBase action in _actions)
            {
                action.Update();
            }
        }
    }
}
