using Gaia.Core.Temp;

namespace Gaia.Core.Actions
{
    /// <summary>
    ///     One thing an option does while it is selected, such as moving to a position or playing an animation. A derived
    ///     action starts its work in Select, advances it in Update once per cycle, stops it in Deselect and reports through
    ///     IsDone when there is nothing left to do.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.4, Listing 8.4</seealso>
    public abstract class ActionBase
    {
        /// <summary>
        ///     True once this action has finished executing. Some actions (such as a looping animation) are never done, but
        ///     others (such as moving to a position) can be completed. The base reports true, so an action that does not override
        ///     this counts as finished from the moment it is selected.
        /// </summary>
        public virtual bool IsDone()
        {
            return true;
        }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public abstract bool Init(AiCreationData creationData);

        /// <summary>Called when the action starts execution.</summary>
        public virtual void Select()
        {
        }

        /// <summary>Called when the action stops execution.</summary>
        public virtual void Deselect()
        {
        }

        /// <summary>Called every cycle while the action is selected.</summary>
        public virtual void Update()
        {
        }
    }
}
