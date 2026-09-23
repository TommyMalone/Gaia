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
        private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Options,
                "One thing the character could do, scored by its considerations and carried out by its actions.")
            .Add(NameAttribute);

        private static readonly SlotDescription ConsiderationSlot = Builder.Slot("Consideration",
            ConceptualAbstraction.Considerations, 0, SlotDescription.Unbounded);

        private static readonly SlotDescription ActionSlot = Builder.Slot("Action", ConceptualAbstraction.Actions, 0,
            SlotDescription.Unbounded);

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Creates an option for the factories to build. Init loads its name and children.</summary>
        internal ConsiderationAndActionOption()
        {
        }

        /// <summary>Creates a named option for a host to assemble in code through AddConsideration and AddAction.</summary>
        /// <remarks>
        ///     An assembled option never runs Init. The default tuning consideration Init supplies to a loaded option arrives
        ///     when the option's reasoner is enabled, so a host adds considerations and actions and nothing more.
        /// </remarks>
        public ConsiderationAndActionOption(AiString name)
        {
            Name = name;
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == ConsiderationSlot.Name)
                {
                    if (!creationData.Factories.Considerations.TryCreate(creationData.ForChild(child),
                            out ConsiderationBase? consideration))
                    {
                        return false;
                    }

                    AddConsideration(consideration!);
                }
                else if (child.Name == ActionSlot.Name)
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
