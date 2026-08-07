using System;
using System.Collections.Generic;
using Gaia.Core.Temp;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>
    ///     Matches the input against a set of configured strings and reports the weight values belonging to the one it
    ///     matched, or DefaultWeights if it matched none.
    /// </summary>
    public class StringWeightFunction : WeightFunctionBase
    {
        private readonly Dictionary<string, WeightValues> _entries = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a function with no entries that reports the given values for every input.</summary>
        internal StringWeightFunction(WeightValues defaultWeights)
        {
            DefaultWeights = defaultWeights;
        }

        /// <summary>The weight values to report for an input matching no entry.</summary>
        public WeightValues DefaultWeights { get; internal set; }

        /// <summary>Adds an entry, replacing any entry already configured for the same string.</summary>
        internal void AddEntry(string value, WeightValues weights)
        {
            _entries[value] = weights;
        }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override WeightValues CalcString(string value)
        {
            return _entries.TryGetValue(value, out WeightValues weights) ? weights : DefaultWeights;
        }
    }
}
