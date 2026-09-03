using Gaia.Core.DataStores;
using Gaia.Core.Factories;
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
        private static readonly AiString BrainStoreName = "Brain";
        private static readonly AiString MissingElement = "Missing";
        private static readonly AiString StoreAttribute = "Store";
        private static readonly AiString VariableAttribute = "Variable";
        private static readonly AiString WorldStoreName = "World";

        /// <summary>The store to read from.</summary>
        public DataStoreBase? DataStore { get; internal set; }

        /// <summary>The name to read.</summary>
        public AiString VariableName { get; internal set; } = AiString.Empty;

        /// <summary>What to report when DataStore is null, or it has nothing under VariableName.</summary>
        /// <remarks>
        ///     Neutral by default, so a variable that has not been written yet leaves its option alone. A configuration that
        ///     would rather veto the option until the value arrives sets this to do so.
        /// </remarks>
        public WeightValues MissingVariableWeights { get; internal set; } = WeightValues.Neutral;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadString(VariableAttribute, out string variableName))
            {
                return false;
            }

            VariableName = variableName;

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
                return MissingVariableWeights;
            }

            return CalculateWeights(weightFunction, DataStore);
        }

        /// <summary>Reads VariableName from the store and converts it, or reports MissingVariableWeights if it is not there.</summary>
        /// <param name="weightFunction">The configured weight function, never null.</param>
        /// <param name="dataStore">The configured store, never null.</param>
        /// <returns>The weight values this consideration reports this cycle.</returns>
        protected abstract WeightValues CalculateWeights(WeightFunctionBase weightFunction, DataStoreBase dataStore);

        /// <summary>Reads the optional Store attribute, which picks between the creation data's blackboards.</summary>
        /// <remarks>The brain blackboard is the default, matching where a character's own knowledge lives.</remarks>
        private bool TryReadStore(SpecificationNode node, AiCreationData creationData)
        {
            DataStore = creationData.BrainBlackboard;

            if (!node.TryGetString(StoreAttribute, out string text))
            {
                return true;
            }

            AiString storeName = text;

            if (storeName == BrainStoreName)
            {
                return true;
            }

            if (storeName == WorldStoreName)
            {
                DataStore = creationData.WorldBlackboard;
                return true;
            }

            creationData.ReportError(
                $"Line {node.LineNumber}, the {node.Name.Text} element's Store is not Brain or World, it reads \"{text}\"");
            return false;
        }
    }
}
