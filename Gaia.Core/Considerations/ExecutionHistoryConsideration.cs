using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Services.Time;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>
    ///     A consideration that reports different values depending on whether its option is running now, has finished,
    ///     has run before, or has never run.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The finished case is how an option ends itself. A reasoner keeps a selected option until a consideration
    ///         vetoes it, and takes no notice of its <see cref="OptionBase.IsDone" />. An option that should stop at that
    ///         point configures a FinishedWeightFunction that vetoes, and an option with none keeps running. Once vetoed the
    ///         option is deselected and the cooldown, if any, starts.
    ///     </para>
    ///     <para>
    ///         The elapsed time functions gate eligibility rather than schedule runs. A cooldown that has passed makes the
    ///         option selectable again and nothing more, so no runs are owed however much time goes by before the reasoner
    ///         next evaluates the option.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class ExecutionHistoryConsideration : ConsiderationBase
    {
        /// <summary>The time the option was most recently deselected, meaningful once _hasRun is true.</summary>
        private Seconds _deselectedTime;

        /// <summary>True once the option has run at least once, which separates never run from ran before.</summary>
        private bool _hasRun;

        /// <summary>The time the option was most recently selected.</summary>
        private Seconds _selectedTime;

        /// <summary>Where the consideration reads the current time from.</summary>
        public ITimeSource TimeSource { get; internal set; } = new SystemTimeSource();

        /// <summary>The rank the option had when it was most recently selected.</summary>
        /// <remarks>Retention needs the rank the option won with, so Select reads Option.Rank and keeps it here.</remarks>
        public float RankWhenLastSelected { get; private set; } = float.MinValue;

        /// <summary>How much of RankWhenLastSelected is kept while Option's actions run, from 0 for none to 1 for all of it.</summary>
        /// <remarks>
        ///     <para>
        ///         None by default. All of it is the Commit pattern, and nothing can then interrupt the option until its actions
        ///         finish. A portion is Inertia, enough to hold off near equals but not something far more important.
        ///     </para>
        ///     <para>
        ///         Retention only applies while the option runs and its <see cref="OptionBase.IsDone" /> is false. Once it is
        ///         true and a FinishedWeightFunction is configured, that function alone decides, since the point of retention is
        ///         to see the actions through.
        ///     </para>
        ///     <para>Only a positive rank is retained, since a portion of a negative rank would be higher than the rank itself.</para>
        /// </remarks>
        public float RankRetentionFactor { get; internal set; }

        /// <summary>Converts the time since the option was selected, while its <see cref="OptionBase.IsDone" /> is false.</summary>
        public WeightFunctionBase? WhileRunningWeightFunction { get; internal set; }

        /// <summary>
        ///     Converts the time since the option was selected, once its <see cref="OptionBase.IsDone" /> is true. Falls back
        ///     to WhileRunningWeightFunction if this is null, so an option that says nothing keeps running after it finishes.
        /// </summary>
        public WeightFunctionBase? FinishedWeightFunction { get; internal set; }

        /// <summary>Reports the values for an option that has not run yet.</summary>
        public WeightFunctionBase? NeverRunWeightFunction { get; internal set; }

        /// <summary>Converts the time since the option was deselected, once it has run and is no longer running.</summary>
        /// <remarks>
        ///     The time runs from the moment the option stopped rather than the moment it started, so a cooldown does not
        ///     shorten however long the option ran.
        /// </remarks>
        public WeightFunctionBase? SinceLastRunWeightFunction { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            if (IsSelected)
            {
                Seconds timeSinceSelected = TimeSource.CurrentTime - _selectedTime;

                Weights = FinishedWeightFunction != null && Option is { IsDone: true }
                    ? Convert(FinishedWeightFunction, timeSinceSelected)
                    : RetainRank(Convert(WhileRunningWeightFunction, timeSinceSelected));
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
        protected override void OnSelect()
        {
            _selectedTime = TimeSource.CurrentTime;

            // Option.Calculate ran this cycle, so Option.Rank is the one it was selected at.
            RankWhenLastSelected = Option?.Rank ?? float.MinValue;

            // Any of the four can be one that rolls fresh values, so selection reaches all of them.
            WhileRunningWeightFunction?.Select();
            FinishedWeightFunction?.Select();
            NeverRunWeightFunction?.Select();
            SinceLastRunWeightFunction?.Select();
        }

        /// <inheritdoc />
        protected override void OnDeselect()
        {
            _deselectedTime = TimeSource.CurrentTime;
            _hasRun = true;

            WhileRunningWeightFunction?.Deselect();
            FinishedWeightFunction?.Deselect();
            NeverRunWeightFunction?.Deselect();
            SinceLastRunWeightFunction?.Deselect();
        }

        /// <summary>
        ///     Raises the rank in weights to the retained portion of RankWhenLastSelected, when that portion is higher than
        ///     the rank the weight function reported.
        /// </summary>
        private WeightValues RetainRank(WeightValues weights)
        {
            if (RankRetentionFactor <= 0f || RankWhenLastSelected <= 0f)
            {
                return weights;
            }

            float retainedRank = RankWhenLastSelected * RankRetentionFactor;

            if (retainedRank <= weights.Rank)
            {
                return weights;
            }

            return new WeightValues(weights.Addend, weights.Multiplier, retainedRank);
        }

        /// <summary>
        ///     Hands the elapsed time to the weight function, or reports neutral values for a case whose weight function was
        ///     not configured.
        /// </summary>
        private static WeightValues Convert(WeightFunctionBase? weightFunction, Seconds elapsedTime)
        {
            return weightFunction?.CalculateFloat(elapsedTime.Value) ?? WeightValues.Neutral;
        }
    }
}
