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
    /// <remarks>
    ///     The actions this library ships have internal constructors, so game code makes one by loading it through
    ///     <see cref="Factories.GaiaLoader" />.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.4, Listing 8.4</seealso>
    public abstract class ActionBase : IAiComponent
    {
        /// <summary>Describes the Name attribute Init reads by on every action, for a derived action's description to add.</summary>
        public static readonly AttributeDescription NameAttribute = AttributeDescription.Required("Name", "Name",
            AttributeKind.String, "What the action is called in the log.");

        /// <summary>What this action is called, which is how its entries are recognized in the log.</summary>
        public AiString Name { get; protected internal set; } = AiString.Empty;

        /// <summary>Where this action reports what it is doing.</summary>
        public ILogSink LogSink { get; set; } = new TraceLogSink();

        /// <summary>The character this action belongs to, which its entries are filed under.</summary>
        public AiString LogChannel { get; set; } = AiString.Empty;

        /// <summary>
        ///     True once this action has finished executing. Some actions (such as a looping animation) are never done, but
        ///     others (such as moving to a position) can be completed.
        /// </summary>
        /// <remarks>
        ///     An action may be deselected without ever having been updated, since a
        ///     <see cref="Reasoners.SequenceReasoner" /> passes a step that is done at selection, and any such steps after it, in
        ///     a single cycle, giving each one Select and Deselect with no Update between. An action that is done from the moment
        ///     it is selected therefore does all of its work in OnSelect.
        /// </remarks>
        public abstract bool IsDone { get; }

        /// <summary>Loads this action's configuration from the creation data's node.</summary>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            if (!creationData.Node.ReadString(NameAttribute.Name, out string name))
            {
                return false;
            }

            Name = name;
            LogSink = creationData.LogSink;
            LogChannel = creationData.LogChannel;

            return OnInit(creationData);
        }

        /// <summary>Loads the derived action's own configuration.</summary>
        /// <remarks>
        ///     The factory has already refused whatever the description does not name, and the default returns true, so an
        ///     action with nothing of its own to load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Starts the action.</summary>
        public void Select()
        {
            if (LogSink.IsEnabled(LogSeverity.Message))
            {
                LogSink.LogMessage(LogChannel, $"{Name.Text} started");
            }

            OnSelect();
        }

        /// <summary>Called when the action starts execution.</summary>
        protected virtual void OnSelect()
        {
        }

        /// <summary>Stops the action.</summary>
        public void Deselect()
        {
            if (LogSink.IsEnabled(LogSeverity.Message))
            {
                LogSink.LogMessage(LogChannel, $"{Name.Text} stopped");
            }

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
