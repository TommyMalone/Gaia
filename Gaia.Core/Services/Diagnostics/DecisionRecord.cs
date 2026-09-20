using Gaia.Core.Strings;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>What one option scored in one recorded cycle.</summary>
    /// <remarks>
    ///     Weight is the option's weight before the screening that <see cref="Options.OptionBase.PassesScreening" />
    ///     reports, so an option the reasoner screened out still shows what it weighed, with PassedScreening saying why it was
    ///     not selected.
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

        /// <summary>What the option's considerations added up to this cycle, before screening.</summary>
        public float Weight { get; }

        /// <summary>False if the reasoner's screening removed the option before the pick.</summary>
        public bool PassedScreening { get; }
    }
}
