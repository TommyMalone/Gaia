using Gaia.Core.Reasoners;
using Gaia.Core.Temp;

namespace Gaia.Core.Actions
{
    /// <summary>
    ///     An action that holds a reasoner of its own. This allows a flat decision to become a hierarchy.
    /// </summary>
    public class SubreasonerAction : ActionBase
    {
        /// <summary>Internal constructor used for testing.</summary>
        /// <param name="reasoner">The reasoner to run while this action is selected.</param>
        internal SubreasonerAction(ReasonerBase reasoner)
        {
            Reasoner = reasoner;
        }

        /// <summary>The reasoner this action runs. Null means the action does nothing and IsDone is immediately true.</summary>
        public ReasonerBase? Reasoner { get; internal set; }

        /// <summary>True if Reasoner is null or reports that it is done.</summary>
        public override bool IsDone => Reasoner?.IsDone ?? true;

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Calls Enable on Reasoner so that it begins making decisions.</summary>
        public override void Select()
        {
            base.Select();
            Reasoner?.Enable();
        }

        /// <summary>Calls Disable on Reasoner, which deselects whatever option it was running.</summary>
        public override void Deselect()
        {
            base.Deselect();
            Reasoner?.Disable();
        }

        /// <summary>Calls Update on Reasoner, so that every level of the tree decides once per cycle.</summary>
        public override void Update()
        {
            base.Update();
            Reasoner?.Update();
        }
    }
}
