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
    public abstract class WeightFunctionBase
    {
        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public abstract bool Init(AiCreationData creationData);

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

        /// <summary>Called when the associated option is selected.</summary>
        public virtual void Select()
        {
        }

        /// <summary>Called when the associated option is deselected.</summary>
        public virtual void Deselect()
        {
        }
    }
}
