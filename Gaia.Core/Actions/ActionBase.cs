using Gaia.Core.Factories;
using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Actions
{
    /// <summary>
    ///     One thing an option does while it is selected, such as moving to a position or playing an animation. A derived
    ///     action starts its work in OnSelect, advances it in OnUpdate once per cycle, stops it in OnDeselect and reports
    ///     through IsDone when there is nothing left to do.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.4, Listing 8.4</seealso>
    public abstract class ActionBase : IAiComponent
    {
        private static readonly AiString NameAttribute = "Name";

        /// <summary>What this action is called, which is how its entries are recognized in the log.</summary>
        public AiString Name { get; internal set; } = AiString.Empty;

        /// <summary>Where this action reports what it is doing.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>The character this action belongs to, which its entries are filed under.</summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>
        ///     True once this action has finished executing. Some actions (such as a looping animation) are never done, but
        ///     others (such as moving to a position) can be completed. The base reports true, so an action that does not override
        ///     this counts as finished from the moment it is selected.
        /// </summary>
        public virtual bool IsDone => true;

        /// <summary>Loads this action's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!creationData.Node.ReadString(NameAttribute, out string name))
            {
                return false;
            }

            Name = name;
            LogSink = creationData.LogSink;
            LogChannel = creationData.LogChannel;

            return OnInit(creationData);
        }

        /// <summary>Loads the derived action's own configuration.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected abstract bool OnInit(AiCreationData creationData);

        /// <summary>Starts the action.</summary>
        public void Select()
        {
            LogSink.LogMessage(LogChannel, $"{Name.Text} started");
            OnSelect();
        }

        /// <summary>Called when the action starts execution.</summary>
        protected virtual void OnSelect()
        {
        }

        /// <summary>Stops the action.</summary>
        public void Deselect()
        {
            LogSink.LogMessage(LogChannel, $"{Name.Text} stopped");
            OnDeselect();
        }

        /// <summary>Called when the action stops execution.</summary>
        protected virtual void OnDeselect()
        {
        }

        /// <summary>Advances the action by one cycle.</summary>
        public void Update()
        {
            OnUpdate();
        }

        /// <summary>Called every cycle while the action is selected.</summary>
        protected virtual void OnUpdate()
        {
        }
    }
}
