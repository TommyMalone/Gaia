using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.Services.Log;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>A consideration that reads a named value out of a data store and hands it to a weight function.</summary>
    /// <remarks>
    ///     <para>
    ///         The store, the name and the weight function are all configured, so the game writes what it knows onto a
    ///         blackboard or an entity and the AI reads it back without either side knowing about the other.
    ///     </para>
    ///     <para>DataStore is a fixed reference for now. The chapter also allows it to come from a target.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.2.3, Listing 8.9</seealso>
    public abstract class VariableConsiderationBase : WeightedConsiderationBase
    {
        private static readonly AiString MissingElement = "Missing";
        private static readonly AiString VariableAttribute = "Variable";

        private bool _missingConfigured;

        private bool _missingVariableReported;

        /// <summary>The store to read from.</summary>
        /// <remarks>
        ///     Settable by a derived type as well as by loading, like <see cref="VariableName" />,
        ///     <see cref="MissingVariableWeights" />, <see cref="LogSink" /> and <see cref="LogChannel" />, so a game's own
        ///     variable consideration built outside the assembly can point at a store of the game's choosing.
        /// </remarks>
        public DataStoreBase? DataStore { get; protected internal set; }

        /// <summary>The name to read.</summary>
        public AiString VariableName { get; protected internal set; } = AiString.Empty;

        /// <summary>What to report when DataStore is null, or it has nothing under VariableName.</summary>
        /// <remarks>
        ///     Neutral by default, so a variable that has not been written yet leaves its option alone. A configuration that
        ///     would rather veto the option until the value arrives sets this to do so.
        /// </remarks>
        public WeightValues MissingVariableWeights { get; protected internal set; } = WeightValues.Neutral;

        /// <summary>Where the never-written warning goes. Loading pulls this from the creation data.</summary>
        public ILogSink LogSink { get; protected internal set; } = new TraceLogSink();

        /// <summary>What the never-written warning is filed under, usually the character the option belongs to.</summary>
        public AiString LogChannel { get; protected internal set; } = AiString.Empty;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadString(VariableAttribute, out string variableName))
            {
                return false;
            }

            VariableName = variableName;
            LogSink = creationData.LogSink;
            LogChannel = creationData.LogChannel;

            if (!TryReadStore(node, creationData))
            {
                return false;
            }

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == MissingElement)
                {
                    if (!WeightValues.TryRead(child, out WeightValues missingWeights))
                    {
                        return false;
                    }

                    MissingVariableWeights = missingWeights;
                    _missingConfigured = true;
                }
                else if (child.Name == WeightFunctionElement)
                {
                    if (!TryLoadWeightFunction(child, creationData))
                    {
                        return false;
                    }
                }
                else
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }
            }

            if (WeightFunction == null)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.Name.Text} element has no WeightFunction element");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction)
        {
            if (DataStore == null)
            {
                return GetMissingVariableWeights();
            }

            return CalculateWeights(weightFunction, DataStore);
        }

        /// <summary>Returns the configured missing values, warning once the first time they are served.</summary>
        /// <remarks>
        ///     <para>
        ///         A typo in a variable name is unreachable by the loader, since names are runtime data, and without this
        ///         warning it reads as a tuning problem. The warning fires once per consideration, and not at all when the
        ///         configuration authored an explicit Missing element, since serving those values is then the designed expectation
        ///         rather than a surprise.
        ///     </para>
        ///     <para>
        ///         The wording names the kind the read asked for, which is the consideration's own query phrase unless the read
        ///         differs, as it does where an entity list answers a bool. A name written as another kind also reads as unwritten
        ///         through the typed accessor, and this warning stands beside the store's own mismatch report, so the read is
        ///         named from both sides.
        ///     </para>
        /// </remarks>
        /// <param name="readKind">
        ///     What the read asked the store for, when that differs from <see cref="QueryPhrase" />, such as an
        ///     entity list read to answer a bool. Null names the query phrase.
        /// </param>
        protected WeightValues GetMissingVariableWeights(string? readKind = null)
        {
            if (!_missingConfigured && !_missingVariableReported)
            {
                _missingVariableReported = true;

                LogSink.LogWarning(LogChannel,
                    $"the {VariableName.Text} variable has never been written as {readKind ?? QueryPhrase}, " +
                    "returning the default missing variable weight values");
            }

            return MissingVariableWeights;
        }

        /// <summary>Reads VariableName from the store and converts it, or reports MissingVariableWeights if it is not there.</summary>
        /// <param name="weightFunction">The configured weight function, never null.</param>
        /// <param name="dataStore">The configured store, never null.</param>
        /// <returns>The weight values this consideration reports this cycle.</returns>
        protected abstract WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore);

        /// <summary>Reads the optional Store attribute through the creation data, which owns the blackboards.</summary>
        private bool TryReadStore(SpecificationNode node, AiCreationData creationData)
        {
            if (!creationData.TryReadStore(node, out DataStoreBase? store))
            {
                return false;
            }

            DataStore = store;
            return true;
        }
    }
}
