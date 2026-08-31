using System;
using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     A conversion from what a consideration measured to the weight values it reports, each derived function giving
    ///     the measurement a different shape.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.2, Listing 8.2</seealso>
    public abstract class WeightFunctionBase : IAiComponent
    {
        /// <summary>Loads this function's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived function's own configuration.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected abstract bool OnInit(AiCreationData creationData);

        /// <summary>Converts a bool, which throws NotSupportedException unless overridden.</summary>
        public virtual WeightValues CalculateBool(bool value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a bool.");
        }

        /// <summary>Converts an int, which forwards to CalculateFloat unless overridden.</summary>
        public virtual WeightValues CalculateInt(int value)
        {
            return CalculateFloat(value);
        }

        /// <summary>Converts a float, which throws NotSupportedException unless overridden.</summary>
        public virtual WeightValues CalculateFloat(float value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a float.");
        }

        /// <summary>Converts a string, which throws NotSupportedException unless overridden.</summary>
        public virtual WeightValues CalculateString(AiString value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a string.");
        }

        /// <summary>Called when the associated option is selected. OnSelect runs.</summary>
        public void Select()
        {
            OnSelect();
        }

        /// <summary>Called by Select, so a derived function can act on the moment.</summary>
        protected virtual void OnSelect()
        {
        }

        /// <summary>Called when the associated option is deselected. OnDeselect runs.</summary>
        public void Deselect()
        {
            OnDeselect();
        }

        /// <summary>Called by Deselect, so a derived function can act on the moment.</summary>
        protected virtual void OnDeselect()
        {
        }
    }
}
