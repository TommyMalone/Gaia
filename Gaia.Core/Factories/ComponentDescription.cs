using System.Collections.Generic;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>The attributes and child elements one element may hold, which the element is held to as it loads.</summary>
    /// <remarks>A component makes its description through a <see cref="DescriptionBuilder" />.</remarks>
    public sealed class ComponentDescription
    {
        /// <summary>Describes a component of the given conceptual abstraction.</summary>
        public ComponentDescription(
            ConceptualAbstraction abstraction,
            string summary,
            IReadOnlyList<AttributeDescription> attributes,
            IReadOnlyList<SlotDescription> slots) : this((ConceptualAbstraction?)abstraction, summary, attributes,
            slots)
        {
        }

        /// <summary>
        ///     Describes a structural element that belongs to no conceptual abstraction, such as the True of a Boolean weight
        ///     function.
        /// </summary>
        public ComponentDescription(
            string summary,
            IReadOnlyList<AttributeDescription> attributes,
            IReadOnlyList<SlotDescription> slots) : this(null, summary, attributes, slots)
        {
        }

        private ComponentDescription(
            ConceptualAbstraction? abstraction,
            string summary,
            IReadOnlyList<AttributeDescription> attributes,
            IReadOnlyList<SlotDescription> slots)
        {
            Abstraction = abstraction;
            Summary = summary;
            Attributes = new List<AttributeDescription>(attributes).AsReadOnly();
            Slots = new List<SlotDescription>(slots).AsReadOnly();
        }

        /// <summary>
        ///     The conceptual abstraction whose factory builds the component, or null for a structural element and for the
        ///     Brain.
        /// </summary>
        public ConceptualAbstraction? Abstraction { get; }

        /// <summary>One line saying what the component does, in a designer's words.</summary>
        public string Summary { get; }

        /// <summary>The attributes the component reads, in the order the component declared them.</summary>
        public IReadOnlyList<AttributeDescription> Attributes { get; }

        /// <summary>The child elements the component takes, in the order the component declared them.</summary>
        public IReadOnlyList<SlotDescription> Slots { get; }

        /// <summary>
        ///     Refuses whatever the creation data's element carries that this description does not name, an attribute, a
        ///     child no slot names, or a child past its slot's MaxCount, and holds each child of an inline slot to that slot's
        ///     description the same way.
        /// </summary>
        /// <remarks>
        ///     The element is walked in document order, attributes first and children after. The first thing refused is
        ///     reported and ends the walk, so the check writes one line and the factories above add theirs. A child of a slot of a
        ///     conceptual abstraction is not entered, since its own factory checks it when the owner's Init builds it.
        /// </remarks>
        /// <param name="creationData">The creation data whose node is the element, and whose sink takes the report.</param>
        /// <param name="refusesType">
        ///     True to refuse a Type on the element, as the Brain and every inline element do, since nothing
        ///     chooses them by a Type. False where a factory chose the component by that Type.
        /// </param>
        /// <returns>True when nothing was refused. False after reporting the first thing that was.</returns>
        internal bool RefuseUndescribed(AiCreationData creationData, bool refusesType)
        {
            return RefuseUndescribed(creationData.Node, creationData, refusesType);
        }

        /// <summary>
        ///     Refuses a slot the creation data's element leaves short of its MinCount, once the owner's Init has taken what
        ///     was there, and holds each child of an inline slot to that slot's description the same way.
        /// </summary>
        /// <remarks>
        ///     The slots are counted in the order the component declared them, and a slot whose MinCount is 0 is never
        ///     counted.
        /// </remarks>
        /// <param name="creationData">The creation data whose node is the element, and whose sink takes the report.</param>
        /// <returns>True when every minimum is met. False after reporting the first slot that falls short.</returns>
        internal bool RefuseShortSlots(AiCreationData creationData)
        {
            return RefuseShortSlots(creationData.Node, creationData);
        }

        /// <summary>Walks one element against this description, entering each inline child with its slot's description.</summary>
        private bool RefuseUndescribed(SpecificationNode node, AiCreationData creationData, bool refusesType)
        {
            if (refusesType && !node.Type.IsEmpty)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element does not take the Type attribute");
                return false;
            }

            for (int index = 0; index < node.AttributeSpellings.Count; index++)
            {
                if (FindAttribute(node.AttributeNameAt(index)) == null)
                {
                    creationData.ReportError(
                        $"Line {node.LineNumber}, the {node.NameSpelling} element does not take the {node.AttributeSpellings[index]} attribute");
                    return false;
                }
            }

            for (int index = 0; index < node.Children.Count; index++)
            {
                SpecificationNode child = node.Children[index];
                SlotDescription? slot = FindSlot(child.Name);

                if (slot == null)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.NameSpelling} element does not take {child.NameSpelling} elements");
                    return false;
                }

                // The Unbounded test saves the count and decides nothing, since Unbounded is the largest integer and no
                // count of earlier siblings reaches it.
                if (slot.MaxCount != SlotDescription.Unbounded &&
                    CountEarlierSiblings(node, index, child.Name) >= slot.MaxCount)
                {
                    creationData.ReportError(slot.MaxCount == 1
                        ? $"Line {child.LineNumber}, the {node.NameSpelling} element takes only one {child.NameSpelling} element"
                        : $"Line {child.LineNumber}, the {node.NameSpelling} element takes at most {slot.MaxCount} {child.NameSpelling} elements");
                    return false;
                }

                if (slot.Inline != null && !slot.Inline.RefuseUndescribed(child, creationData, true))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Counts one element's children against its slots' minimums, entering each inline child with its slot's
        ///     description.
        /// </summary>
        private bool RefuseShortSlots(SpecificationNode node, AiCreationData creationData)
        {
            for (int index = 0; index < Slots.Count; index++)
            {
                SlotDescription slot = Slots[index];

                // The skip saves the count and decides nothing, since no count of children is fewer than zero.
                if (slot.MinCount == 0)
                {
                    continue;
                }

                int count = CountEarlierSiblings(node, node.Children.Count, slot.Name);

                if (count >= slot.MinCount)
                {
                    continue;
                }

                creationData.ReportError(slot.MinCount == 1
                    ? $"Line {node.LineNumber}, the {node.NameSpelling} element has no {slot.Spelling} element"
                    : $"Line {node.LineNumber}, the {node.NameSpelling} element has fewer than {slot.MinCount} {slot.Spelling} elements");
                return false;
            }

            for (int index = 0; index < node.Children.Count; index++)
            {
                SpecificationNode child = node.Children[index];

                if (FindSlot(child.Name) is { Inline: { } inline } && !inline.RefuseShortSlots(child, creationData))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The attribute of the given name, or null when no attribute carries it.</summary>
        private AttributeDescription? FindAttribute(AiString name)
        {
            for (int index = 0; index < Attributes.Count; index++)
            {
                if (Attributes[index].Name == name)
                {
                    return Attributes[index];
                }
            }

            return null;
        }

        /// <summary>The slot of the given name, or null when no slot carries it.</summary>
        private SlotDescription? FindSlot(AiString name)
        {
            for (int index = 0; index < Slots.Count; index++)
            {
                if (Slots[index].Name == name)
                {
                    return Slots[index];
                }
            }

            return null;
        }

        /// <summary>How many of the element's children before the given index carry the given name.</summary>
        private static int CountEarlierSiblings(SpecificationNode node, int index, AiString name)
        {
            int count = 0;

            for (int earlier = 0; earlier < index; earlier++)
            {
                if (node.Children[earlier].Name == name)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
