using System.Collections.Generic;
using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Strings;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Options
{
    /// <summary>
    ///     One thing an AI could choose to do, pairing the considerations that score it with the actions that run if it
    ///     is chosen.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.3.1, Listing 8.11</seealso>
    public abstract class OptionBase : IAiComponent
    {
        private static readonly AiString NameAttribute = "Name";

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

        /// <summary>True if this option can be selected, which is whenever its Weight is above 0.</summary>
        public bool IsValid => Weight > 0f;

        /// <summary>
        ///     The weight this option must reach to survive, set by <see cref="DualUtilityReasoner" /> from the best weight
        ///     it found across all of its options, scaled by the reasoner's
        ///     <see cref="DualUtilityReasoner.WeightScreeningFactor" />.
        /// </summary>
        public float ScreeningWeight { get; internal set; }

        /// <summary>
        ///     The rank this option must reach to survive, set by the reasoner from the best rank it found across all of its
        ///     options.
        /// </summary>
        public float ScreeningRank { get; internal set; } = float.MinValue;

        /// <summary>True unless the reasoner's comparison against the other options screened this option out.</summary>
        /// <remarks>
        ///     An option below either ScreeningRank or ScreeningWeight reports a Weight of 0 rather than being taken out of a
        ///     list, which is how a reasoner narrows its options without building a subset. <see cref="DualUtilityReasoner" /> is
        ///     presently the only reasoner that sets these thresholds. For the other reasoner types PassesScreening is always
        ///     true.
        /// </remarks>
        private bool PassesScreening =>
            _considerations.Rank >= ScreeningRank && _considerations.Weight >= ScreeningWeight;

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
            if (!creationData.Node.ReadString(NameAttribute, out string name))
            {
                return false;
            }

            Name = name;

            if (!OnInit(creationData))
            {
                return false;
            }

            EnsureTuningConsideration();
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
        ///         The default contributes an addend of 1 and a multiplier of 1. An option whose other considerations only
        ///         multiply would otherwise sum its addends to 0, leaving a weight of 0, and could never be selected.
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

            WeightValues neutralTuning = new(1f, 1f, float.MinValue);
            AddConsideration(new TuningConsideration(neutralTuning));
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
