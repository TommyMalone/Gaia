using Gaia.Core.Factories;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Actions
{
    /// <summary>An action that reports that it is done once its Duration has passed.</summary>
    /// <seealso>"Modular AI", Section 8.5.4</seealso>
    public sealed class PauseAction : ActionBase
    {
        private static readonly AiString DurationAttribute = "Duration";

        /// <summary>True while the action is selected.</summary>
        private bool _isRunning;

        /// <summary>The time the action was selected, which Duration is measured from.</summary>
        private Seconds _startTime;

        /// <summary>Creates a pause of no duration, so that it is done immediately until a configuration loads.</summary>
        internal PauseAction() : this(new Seconds(0f))
        {
        }

        /// <summary>Creates a pause of the given duration.</summary>
        /// <param name="duration">How long the action waits before reporting that it is done.</param>
        internal PauseAction(Seconds duration)
        {
            Duration = duration;
        }

        /// <summary>How long the action waits before reporting that it is done.</summary>
        public Seconds Duration { get; internal set; }

        /// <summary>Where the action reads the current time from.</summary>
        public ITimeSource TimeSource { get; internal set; } = new SystemTimeSource();

        /// <summary>True once Duration has passed since the action was selected, or while the action is not selected.</summary>
        public override bool IsDone
        {
            get
            {
                if (!_isRunning)
                {
                    return true;
                }

                return TimeSource.CurrentTime - _startTime >= Duration;
            }
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            TimeSource = creationData.TimeSource;

            if (node.Children.Count > 0)
            {
                SpecificationNode child = node.Children[0];

                creationData.ReportError(
                    $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                return false;
            }

            if (!node.ReadFloat(DurationAttribute, out float duration))
            {
                return false;
            }

            Duration = new Seconds(duration);
            return true;
        }

        /// <inheritdoc />
        protected override void OnSelect()
        {
            _startTime = TimeSource.CurrentTime;
            _isRunning = true;
        }

        /// <inheritdoc />
        protected override void OnDeselect()
        {
            _isRunning = false;
        }
    }
}
