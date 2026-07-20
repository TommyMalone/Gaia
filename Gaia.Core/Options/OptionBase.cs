using System.Collections.Generic;
using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Temp;

namespace Gaia.Core.Options
{
    public abstract class OptionBase
    {
        private readonly List<ActionBase> _actions = new();
        private readonly ConsiderationSet _considerations = new();

        /// <summary>True while this option is the one its reasoner has selected.</summary>
        public bool IsSelected { get; private set; }

        public float Weight => _considerations.Weight;
        public float Rank => _considerations.Rank;
        /// <summary>True if this option can be selected, which is whenever its Weight is above 0.</summary>
        public bool IsValid => Weight > 0f;

        public float ScreeningWeight
        {
            get => _considerations.ScreeningWeight;
            set => _considerations.ScreeningWeight = value;
        }

        public float ScreeningRank
        {
            get => _considerations.ScreeningRank;
            set => _considerations.ScreeningRank = value;
        }

        /// <summary>True once every action on this option reports that it is done.</summary>
        public bool IsDone
        {
            get
            {
                foreach (ActionBase action in _actions)
                {
                    if (!action.IsDone())
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        ///     Load the configuration from data. Not virtual, so that every option is guaranteed to run EnsureDefaultTuning
        ///     once its configuration is complete. Derived options override OnInit.
        /// </summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!OnInit(creationData))
            {
                return false;
            }

            EnsureDefaultTuning();
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
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

        /// <summary>
        ///     GAIA adds a default Tuning consideration with an addend of 1 when the configuration does not specify one.
        ///     Without it every addend is 0, so the weight is 0 and the option can never be selected.
        /// </summary>
        private void EnsureDefaultTuning()
        {
            if (_considerations.HasTuning)
            {
                return;
            }

            AddConsideration(new TuningConsideration());
        }

        public void Calculate()
        {
            _considerations.Calculate();
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
