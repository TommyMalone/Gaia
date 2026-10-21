using Gaia.Core.Strings;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>What one option scored in one recorded cycle.</summary>
    /// <remarks>
    ///     An option the reasoner screened out still shows what it weighed, and its PassedScreening says screening is why
    ///     it was not selected.
    /// </remarks>
    public readonly struct DecisionRecord
    {
        /// <summary>
        ///     Records what one option reported in a cycle. <see cref="ReasonerMonitor.OnCycle" /> constructs one for each
        ///     option and hands it to the history.
        /// </summary>
        internal DecisionRecord(AiString optionName, float rank, float weight, bool passedScreening)
        {
            OptionName = optionName;
            Rank = rank;
            Weight = weight;
            PassedScreening = passedScreening;
        }

        /// <summary>The option's name, as its reasoner reports it.</summary>
        public AiString OptionName { get; }

        /// <summary>The highest rank the option's considerations reported this cycle.</summary>
        public float Rank { get; }

        /// <summary>
        ///     What the option's considerations came to this cycle, their addends' sum scaled by their multipliers' product,
        ///     before screening.
        /// </summary>
        public float Weight { get; }

        /// <summary>
        ///     False if Rank fell below the option's <see cref="Options.OptionBase.ScreeningRank" /> or Weight below its
        ///     <see cref="Options.OptionBase.ScreeningWeight" />, so its reasoner counted it as weighing 0 when it selected. Only
        ///     a DualUtilityReasoner sets the thresholds, so under any other reasoner it is false only for an option whose weight
        ///     came out below 0.
        /// </summary>
        public bool PassedScreening { get; }
    }
}
