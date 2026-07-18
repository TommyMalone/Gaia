using Gaia.Core.Temp;

namespace Gaia.Core.Considerations
{
    public abstract class ConsiderationBase
    {
        // These are GAIA's weight values. They return the results computed by calculate.

        public WeightValues Weights { get; protected set; } = WeightValues.Neutral;

        public float Addend => Weights.Addend;
        public float Multiplier => Weights.Multiplier;
        public float Rank => Weights.Rank;

        public bool IsSelected { get; private set; }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public abstract bool Init(AiCreationData creationData);

        /// <summary>
        ///     Called once per decision cycle, allows the consideration to evaluate the situation and determine what to
        ///     return.
        /// </summary>
        public abstract void Calculate();


        // Certain considerations need to know if/when they are selected/deselected.

        /// <summary>Called when the option this consideration scores is selected. Sets IsSelected.</summary>
        public virtual void Select()
        {
            IsSelected = true;
        }

        /// <summary>Called when the option this consideration scores is deselected. Clears IsSelected.</summary>
        public virtual void Deselect()
        {
            IsSelected = false;
        }
    }
}
