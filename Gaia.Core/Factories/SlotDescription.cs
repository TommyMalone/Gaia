using System;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>One kind of child element a component reads, described for a tool that offers what may be added there.</summary>
    /// <remarks>
    ///     A slot of a conceptual abstraction holds a component the factory of that abstraction builds, picked by the
    ///     child's Type. An inline slot holds a structural element the owner parses itself, with no Type, and carries that
    ///     element's description. The True and False elements of a Boolean weight function each fill an inline slot.
    /// </remarks>
    public sealed class SlotDescription
    {
        /// <summary>The MaxCount of a slot that takes any number of children.</summary>
        public const int Unbounded = int.MaxValue;

        /// <summary>Describes a slot filled by a component of the given conceptual abstraction.</summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public SlotDescription(
            AiString name,
            string spelling,
            ConceptualAbstraction abstraction,
            int minCount,
            int maxCount) : this(name, spelling, abstraction, null, minCount, maxCount)
        {
        }

        /// <summary>Describes a slot filled by a structural element the owner parses itself, with the given description.</summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public SlotDescription(AiString name, string spelling, ComponentDescription inline, int minCount, int maxCount)
            : this(name, spelling, null, inline, minCount, maxCount)
        {
        }

        /// <summary>Keeps what both public constructors share, the check on the spelling.</summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        private SlotDescription(
            AiString name,
            string spelling,
            ConceptualAbstraction? abstraction,
            ComponentDescription? inline,
            int minCount,
            int maxCount)
        {
            AiString spelled = spelling;

            if (spelled != name)
            {
                throw new ArgumentException($"The spelling \"{spelling}\" does not spell the element name {name.Text}.",
                    nameof(spelling));
            }

            Name = name;
            Spelling = spelling;
            Abstraction = abstraction;
            Inline = inline;
            MinCount = minCount;
            MaxCount = maxCount;
        }

        /// <summary>The element name the component reads the children by.</summary>
        public AiString Name { get; }

        /// <summary>The element name as a document writes it.</summary>
        public string Spelling { get; }

        /// <summary>The conceptual abstraction whose factory builds a child of this slot, or null for an inline slot.</summary>
        public ConceptualAbstraction? Abstraction { get; }

        /// <summary>The description of the structural element this slot holds, or null for a slot of a conceptual abstraction.</summary>
        public ComponentDescription? Inline { get; }

        /// <summary>The fewest children the slot takes, 0 for an optional child.</summary>
        public int MinCount { get; }

        /// <summary>The most children the slot takes, or <see cref="Unbounded" />.</summary>
        public int MaxCount { get; }
    }
}
