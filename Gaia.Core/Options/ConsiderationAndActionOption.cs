using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Options
{
    /// <summary>The standard option, loading the considerations that score it and the actions it runs from its children.</summary>
    /// <remarks>
    ///     Children load in document order, so the order the actions run in is the order they are written in. An option
    ///     with no Consideration children still scores, through the default <see cref="TuningConsideration" /> that
    ///     <see cref="OptionBase.Init" /> ensures.
    /// </remarks>
    /// <seealso>"Modular AI", Listing 8.11</seealso>
    public sealed class ConsiderationAndActionOption : OptionBase
    {
        private static readonly AiString ActionElement = "Action";
        private static readonly AiString ConsiderationElement = "Consideration";

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == ConsiderationElement)
                {
                    if (!creationData.Factories.Considerations.TryCreate(creationData.ForChild(child),
                            out ConsiderationBase? consideration))
                    {
                        return false;
                    }

                    AddConsideration(consideration!);
                }
                else if (child.Name == ActionElement)
                {
                    if (!creationData.Factories.Actions.TryCreate(creationData.ForChild(child), out ActionBase? action))
                    {
                        return false;
                    }

                    AddAction(action!);
                }
                else
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }
            }

            return true;
        }
    }
}
