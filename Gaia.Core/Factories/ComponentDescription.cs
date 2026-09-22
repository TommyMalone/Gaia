using System.Collections.Generic;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     What one element may hold, the attributes a component reads from it and the child elements it takes, described
    ///     for a tool that edits a document without loading it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every registered component carries one, passed to <see cref="ComponentFactory{TBase}.Register" />, and a
    ///         shipped component declares it as a static beside the <see cref="AiString" /> name fields its Init reads by,
    ///         built from those same fields. A structural element an owner parses itself, such as a Keyframe, is described the
    ///         same way, inline on the slot that holds it, and belongs to no conceptual abstraction.
    ///     </para>
    ///     <para>The attributes are in the order a tool lists them, and the slots in the order a document writes them.</para>
    /// </remarks>
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

        /// <summary>The attributes the component reads, in the order a tool lists them.</summary>
        public IReadOnlyList<AttributeDescription> Attributes { get; }

        /// <summary>The child elements the component takes, in the order a document writes them.</summary>
        public IReadOnlyList<SlotDescription> Slots { get; }
    }
}
