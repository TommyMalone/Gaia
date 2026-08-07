using Gaia.Core.Services.Time;
using Gaia.Core.Temp;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     A consideration that reports different values depending on whether its option is running now, has run before,
    ///     or has never run.
    /// </summary>
    /// <remarks>
    ///     Each of the three cases has a weight function of its own. The while running function is given the time since
    ///     the option was selected, and the since last run function is given the time since it was deselected, so a cooldown
    ///     is measured from the moment the option stopped rather than from the moment it started.
    /// </remarks>
    public class ExecutionHistoryConsideration : ConsiderationBase
    {
        /// <summary>The time the option was most recently deselected, meaningful once _hasRun is true.</summary>
        private Seconds _deselectedTime;

        /// <summary>True once the option has run at least once, which separates never run from ran before.</summary>
        private bool _hasRun;

        /// <summary>The time the option was most recently selected.</summary>
        private Seconds _selectedTime;

        /// <summary>Where the consideration reads the current time from.</summary>
        public ITimeSource TimeSource { get; internal set; } = new SystemTimeSource();

        /// <summary>Converts the time the option has been running.</summary>
        public WeightFunctionBase? WhileRunningWeightFunction { get; internal set; }

        /// <summary>Reports the values for an option that has not run yet.</summary>
        public WeightFunctionBase? NeverRunWeightFunction { get; internal set; }

        /// <summary>Converts the time since the option was deselected, once it has run and is no longer running.</summary>
        /// <remarks>
        ///     The time runs from the moment the option stopped rather than the moment it started, so a cooldown does not
        ///     shorten however long the option ran.
        /// </remarks>
        public WeightFunctionBase? SinceLastRunWeightFunction { get; internal set; }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            if (IsSelected)
            {
                Weights = Convert(WhileRunningWeightFunction, TimeSource.CurrentTime - _selectedTime);
            }
            else if (!_hasRun)
            {
                // Nothing has elapsed that a weight function could read, so this case is normally a constant.
                Weights = Convert(NeverRunWeightFunction, Seconds.Zero);
            }
            else
            {
                Weights = Convert(SinceLastRunWeightFunction, TimeSource.CurrentTime - _deselectedTime);
            }
        }

        /// <inheritdoc />
        public override void Select()
        {
            base.Select();

            _selectedTime = TimeSource.CurrentTime;

            // Any of the three can be one that rolls fresh values, so all of them hear about selection.
            WhileRunningWeightFunction?.Select();
            NeverRunWeightFunction?.Select();
            SinceLastRunWeightFunction?.Select();
        }

        /// <inheritdoc />
        public override void Deselect()
        {
            base.Deselect();

            _deselectedTime = TimeSource.CurrentTime;
            _hasRun = true;

            WhileRunningWeightFunction?.Deselect();
            NeverRunWeightFunction?.Deselect();
            SinceLastRunWeightFunction?.Deselect();
        }

        /// <summary>
        ///     Hands the elapsed time to the weight function, or reports neutral values for a case whose weight function was
        ///     not configured.
        /// </summary>
        private static WeightValues Convert(WeightFunctionBase? weightFunction, Seconds elapsedTime)
        {
            return weightFunction?.CalcFloat(elapsedTime.Value) ?? WeightValues.Neutral;
        }
    }
}
