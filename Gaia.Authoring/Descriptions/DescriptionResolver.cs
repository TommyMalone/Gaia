using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Authoring.Descriptions
{
    /// <summary>
    ///     Finds the <see cref="ComponentDescription" /> of an element of a document and the
    ///     <see cref="SlotDescription" /> of its parent that it fills, from the descriptions a <see cref="FactorySet" />
    ///     holds.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The walk follows <see cref="SpecificationNode.Parent" /> from the element up to the root of its tree and
    ///         comes back down one child at a time. A root named <see cref="FactorySet.RootName" /> resolves to
    ///         <see cref="FactorySet.Root" />. Each child below it resolves through the slot of its parent's description that
    ///         takes its element name. An inline slot gives the description it carries, and a slot of a
    ///         <see cref="ConceptualAbstraction" /> gives the description the factory of that abstraction registered under the
    ///         child's Type. The first element that resolves to no description says why in its <see cref="Resolution.Kind" />,
    ///         and every element beneath it answers <see cref="ResolutionKind.BeneathUndescribed" />.
    ///     </para>
    ///     <para>
    ///         Element names and Types are compared as <see cref="AiString" /> values, in any casing, as loading compares
    ///         them. A child therefore resolves through the slot that loading reads it by. Nothing is kept between calls. Each
    ///         call walks the element's ancestors again, so it answers from the tree and the set as they stand when it is
    ///         made, after any edit or registration.
    ///     </para>
    /// </remarks>
    public static class DescriptionResolver
    {
        /// <summary>The element name of the root that <see cref="FactorySet.Root" /> describes.</summary>
        private static readonly AiString RootName = FactorySet.RootName;

        /// <summary>
        ///     Resolves an element to its description and the slot of its parent that it fills, or to the reason it has no
        ///     description.
        /// </summary>
        public static Resolution Resolve(FactorySet factories, SpecificationNode node)
        {
            SpecificationNode? parent = node.Parent;

            if (parent == null)
            {
                return node.Name == RootName
                    ? new Resolution(ResolutionKind.Described, factories.Root, null)
                    : new Resolution(ResolutionKind.NoSlot, null, null);
            }

            ComponentDescription? parentDescription = Resolve(factories, parent).Description;

            return parentDescription == null
                ? new Resolution(ResolutionKind.BeneathUndescribed, null, null)
                : ResolveChild(factories, parentDescription, node);
        }

        /// <summary>
        ///     Resolves an element whose parent resolved to the given description, through the slot of that description that
        ///     takes the element's name.
        /// </summary>
        private static Resolution ResolveChild(
            FactorySet factories,
            ComponentDescription parentDescription,
            SpecificationNode node)
        {
            SlotDescription? slot = FindSlot(parentDescription, node.Name);

            if (slot == null)
            {
                return new Resolution(ResolutionKind.NoSlot, null, null);
            }

            if (slot.Abstraction == null)
            {
                return new Resolution(ResolutionKind.Described, slot.Inline, slot);
            }

            if (node.Type.IsEmpty)
            {
                return new Resolution(ResolutionKind.NoType, null, slot);
            }

            IComponentFactory factory = factories.Factory(slot.Abstraction.Value);

            if (!factory.TryGetDescription(node.Type, out ComponentDescription? description))
            {
                return new Resolution(ResolutionKind.UnknownType, null, slot);
            }

            return new Resolution(ResolutionKind.Described, description, slot);
        }

        /// <summary>The slot of the given name, or null when no slot of the description carries it.</summary>
        private static SlotDescription? FindSlot(ComponentDescription description, AiString name)
        {
            for (int index = 0; index < description.Slots.Count; index++)
            {
                if (description.Slots[index].Name == name)
                {
                    return description.Slots[index];
                }
            }

            return null;
        }
    }
}
