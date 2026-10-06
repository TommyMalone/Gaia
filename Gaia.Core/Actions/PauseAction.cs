using Gaia.Core.Factories;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;

namespace Gaia.Core.Actions
{
    /// <summary>An action that reports that it is done once its Duration has passed.</summary>
    /// <seealso>"Modular AI", Section 8.5.4</seealso>
    public sealed class PauseAction : ActionBase
    {
        private static readonly DescriptionBuilder Builder =
            new DescriptionBuilder(ConceptualAbstraction.Actions, "Waits for a set time, then reports done.").Add(
                NameAttribute);

        private static readonly AttributeDescription DurationAttribute = Builder.Required("Duration",
            AttributeKind.Float, "How long to wait, in seconds.");

        internal static readonly ComponentDescription Description = Builder.Build();

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

            if (!node.ReadFloat(DurationAttribute.Name, out float duration))
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
