using Gaia.Core.Blackboards;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Brains
{
    /// <summary>The decision-making logic of one character, holding its top-level reasoner and a blackboard of its own.</summary>
    /// <remarks>
    ///     The chapter describes the brain only in passing, as the thing an actor contains and the thing its reasoner
    ///     reports to, so what it holds has been derived from what the rest of the architecture needs from it. A
    ///     <see cref="BrainBlackboard" /> is where contacts and threats live and where components of one character share
    ///     values, so the brain owns it. RootReasoner is what makes the character act at all, so the brain enables and updates
    ///     it.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.3.1</seealso>
    public class Brain
    {
        /// <summary>The values the components of this character share among themselves.</summary>
        public BrainBlackboard Blackboard { get; } = new();

        /// <summary>The reasoner at the top of this character's decision tree. Null means the brain decides nothing.</summary>
        public ReasonerBase? RootReasoner { get; internal set; }

        /// <summary>Where this brain reports taking control of its character and giving it up.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>The character this brain belongs to, which its entries are filed under.</summary>
        public AiString LogChannel { get; internal set; } = AiString.Empty;

        /// <summary>True while the brain is in control of its character.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>Load the configuration from data. Not yet implemented.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        public bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Hands control to the brain, which starts RootReasoner deciding.</summary>
        /// <remarks>
        ///     Enabling and disabling live here rather than on the actor so that a character can keep its actor while the
        ///     player has control, with its brain disabled, or with no brain at all.
        /// </remarks>
        public void Enable()
        {
            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;

            if (RootReasoner == null)
            {
                LogSink.LogWarning(LogChannel, "brain enabled with no root reasoner");
            }
            else
            {
                LogSink.LogMessage(LogChannel, "brain enabled");
            }

            RootReasoner?.Enable();
        }

        /// <summary>Takes control away from the brain, which stops whatever RootReasoner was running.</summary>
        /// <remarks>
        ///     Disable disables RootReasoner, which deselects whatever it was running, so the whole tree beneath it is torn
        ///     down in one call.
        /// </remarks>
        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            LogSink.LogMessage(LogChannel, "brain disabled");
            RootReasoner?.Disable();
        }

        /// <summary>Runs one decision cycle, if IsEnabled.</summary>
        public void Update()
        {
            if (!IsEnabled)
            {
                return;
            }

            RootReasoner?.Update();
        }
    }
}
