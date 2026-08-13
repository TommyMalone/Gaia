using Gaia.Core.Services.Time;
using Gaia.Core.Temp;

namespace Gaia.Core.Actions
{
    /// <summary>An action that reports that it is done once its Duration has passed.</summary>
    /// <seealso>"Modular AI", Section 8.5.4</seealso>
    public sealed class PauseAction : ActionBase
    {
        /// <summary>True while the action is selected.</summary>
        private bool _isRunning;

        /// <summary>The time the action was selected, which Duration is measured from.</summary>
        private Seconds _startTime;

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
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Select()
        {
            base.Select();

            _startTime = TimeSource.CurrentTime;
            _isRunning = true;
        }

        /// <inheritdoc />
        public override void Deselect()
        {
            base.Deselect();

            _isRunning = false;
        }
    }
}
