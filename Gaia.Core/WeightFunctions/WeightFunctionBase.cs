using System;
using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     A conversion from the value a <see cref="Considerations.WeightedConsiderationBase" /> measured to the weight
    ///     values it reports, each derived function giving the measurement a different shape.
    /// </summary>
    /// <remarks>
    ///     A shipped function is made by loading rather than by construction from game code, since its values load
    ///     through Init and its setters are internal. Loading through <see cref="Factories.GaiaLoader" /> is the public way to
    ///     make one. A game's own derived functions belong to the game and expose whatever constructors it wants.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2, Listing 8.2</seealso>
    public abstract class WeightFunctionBase : IAiComponent
    {
        /// <summary>True when CalculateBool converts rather than throws.</summary>
        /// <remarks>
        ///     The conversion flags are how loading refuses a function wired to a consideration it cannot serve, turning a
        ///     decision-path exception into a load error naming the file and line. A derived function overrides the flags for
        ///     exactly the conversions it overrides, and a game's own considerations may consult them the same way.
        /// </remarks>
        public virtual bool ConvertsBool => false;

        /// <summary>True when CalculateInt converts, following ConvertsFloat the way CalculateInt forwards.</summary>
        public virtual bool ConvertsInt => ConvertsFloat;

        /// <summary>True when CalculateFloat converts rather than throws.</summary>
        public virtual bool ConvertsFloat => false;

        /// <summary>True when CalculateString converts rather than throws.</summary>
        public virtual bool ConvertsString => false;

        /// <summary>Loads this function's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            return OnInit(creationData);
        }

        /// <summary>Loads the derived function's own configuration.</summary>
        /// <remarks>
        ///     The base refuses any child element and otherwise reports true, so a weight function with nothing of its own to
        ///     load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return creationData.RefuseChildren();
        }

        /// <summary>Converts a bool.</summary>
        /// <exception cref="NotSupportedException">This function does not convert a bool.</exception>
        public virtual WeightValues CalculateBool(bool value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a bool.");
        }

        /// <summary>Converts an int, which forwards to CalculateFloat unless overridden.</summary>
        public virtual WeightValues CalculateInt(int value)
        {
            return CalculateFloat(value);
        }

        /// <summary>Converts a float.</summary>
        /// <exception cref="NotSupportedException">This function does not convert a float.</exception>
        public virtual WeightValues CalculateFloat(float value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a float.");
        }

        /// <summary>Converts a string.</summary>
        /// <exception cref="NotSupportedException">This function does not convert a string.</exception>
        public virtual WeightValues CalculateString(AiString value)
        {
            throw new NotSupportedException($"{GetType().Name} cannot convert a string.");
        }

        /// <summary>
        ///     Called when the <see cref="Options.OptionBase" /> holding the consideration this function scores is selected.
        ///     OnSelect runs.
        /// </summary>
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
