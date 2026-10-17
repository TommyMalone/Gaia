using System;
using System.Collections.Generic;
using Gaia.Core.DataStores;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>Collects the attributes and slots a component declares as fields and seals them into its description.</summary>
    /// <remarks>
    ///     <para>
    ///         A component declares one builder, then one <see cref="AttributeDescription" /> or
    ///         <see cref="SlotDescription" /> field for each name it loads by, made through the builder, and its description
    ///         last, as the builder's <see cref="Build" />. C# runs a class's static field initializers in the order they are
    ///         written, so every field above Build is in the description. Init then reads by RadiusAttribute.Name, which makes
    ///         the description the one place a name is written.
    ///     </para>
    ///     <para>
    ///         The spelling is the one argument, and the builder makes the <see cref="AiString" /> from it at static
    ///         initialization, which keeps the string table's lock off the cycle path.
    ///     </para>
    ///     <para>
    ///         <see cref="Add(AttributeDescription)" /> takes a piece another type owns, such as the Store attribute
    ///         <see cref="AiCreationData" /> reads, and records it in the same order.
    ///     </para>
    /// </remarks>
    public sealed class DescriptionBuilder
    {
        private readonly ConceptualAbstraction? _abstraction;
        private readonly List<AttributeDescription> _attributes = new();
        private readonly List<SlotDescription> _slots = new();
        private readonly string _summary;

        private bool _isBuilt;

        /// <summary>Starts the description of a component one of the conceptual abstractions builds.</summary>
        /// <param name="abstraction">The conceptual abstraction whose factory builds the component.</param>
        /// <param name="summary">One line saying what the component does, in a designer's words.</param>
        public DescriptionBuilder(ConceptualAbstraction abstraction, string summary)
        {
            _abstraction = abstraction;
            _summary = summary;
        }

        /// <summary>Starts the description of a structural element its owner parses itself, such as a Keyframe.</summary>
        /// <param name="summary">One line saying what the element holds, in a designer's words.</param>
        public DescriptionBuilder(string summary)
        {
            _summary = summary;
        }

        /// <summary>Describes and records a required attribute of one of the seven typed kinds.</summary>
        /// <param name="spelling">The name as a document writes it.</param>
        /// <param name="kind">What the attribute is read as.</param>
        /// <param name="summary">One line saying what the attribute does, in a designer's words.</param>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public AttributeDescription Required(string spelling, AttributeKind kind, string summary)
        {
            return Record(AttributeDescription.Required(spelling, spelling, kind, summary));
        }

        /// <summary>Describes and records an optional attribute of one of the seven typed kinds.</summary>
        /// <param name="spelling">The name as a document writes it.</param>
        /// <param name="kind">What the attribute is read as.</param>
        /// <param name="defaultText">The value an absent attribute reads as, or null where absence means no value at all.</param>
        /// <param name="summary">One line saying what the attribute does, in a designer's words.</param>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public AttributeDescription Optional(string spelling, AttributeKind kind, string? defaultText, string summary)
        {
            return Record(AttributeDescription.Optional(spelling, spelling, kind, defaultText, summary));
        }

        /// <summary>Describes and records an optional attribute read as one of the enumeration's names.</summary>
        /// <param name="spelling">The name as a document writes it.</param>
        /// <param name="defaultText">The name an absent attribute reads as, which is one of the enumeration's.</param>
        /// <param name="summary">One line saying what the attribute does, in a designer's words.</param>
        /// <typeparam name="TEnum">The enumeration whose names are the attribute's choices.</typeparam>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        /// <exception cref="ArgumentException">
        ///     The enumeration carries the Flags attribute, has no members, or has two member
        ///     names that differ only in casing.
        /// </exception>
        public AttributeDescription Enumeration<TEnum>(string spelling, string defaultText, string summary)
            where TEnum : struct, Enum
        {
            return Record(AttributeDescription.Enumeration<TEnum>(spelling, spelling, defaultText, summary));
        }

        /// <summary>Describes and records a required attribute naming a stored value that is read as the given kind.</summary>
        /// <param name="spelling">The name as a document writes it.</param>
        /// <param name="variableKind">The kind of stored value the name is read as.</param>
        /// <param name="summary">One line saying what the attribute does, in a designer's words.</param>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public AttributeDescription VariableName(string spelling, DataStoreValueType variableKind, string summary)
        {
            return Record(AttributeDescription.VariableName(spelling, spelling, variableKind, summary));
        }

        /// <summary>Describes and records a slot filled by a component of the given conceptual abstraction.</summary>
        /// <param name="spelling">The element name as a document writes it.</param>
        /// <param name="abstraction">The conceptual abstraction whose factory builds a child of this slot.</param>
        /// <param name="minCount">The fewest children the slot takes, 0 for an optional child.</param>
        /// <param name="maxCount">The most children the slot takes, or <see cref="SlotDescription.Unbounded" />.</param>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public SlotDescription Slot(string spelling, ConceptualAbstraction abstraction, int minCount, int maxCount)
        {
            return Record(new SlotDescription(spelling, spelling, abstraction, minCount, maxCount));
        }

        /// <summary>Describes and records a slot filled by a structural element this component parses itself.</summary>
        /// <param name="spelling">The element name as a document writes it.</param>
        /// <param name="inline">The description of the structural element the slot holds.</param>
        /// <param name="minCount">The fewest children the slot takes, 0 for an optional child.</param>
        /// <param name="maxCount">The most children the slot takes, or <see cref="SlotDescription.Unbounded" />.</param>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public SlotDescription Slot(string spelling, ComponentDescription inline, int minCount, int maxCount)
        {
            return Record(new SlotDescription(spelling, spelling, inline, minCount, maxCount));
        }

        /// <summary>Records an attribute another type owns and reads by, such as the Store attribute of the creation data.</summary>
        /// <returns>This builder, so that several shared pieces are recorded in one expression.</returns>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public DescriptionBuilder Add(AttributeDescription attribute)
        {
            Record(attribute);

            return this;
        }

        /// <summary>Records a set of attributes another type owns and reads, such as the four of a set of weight values.</summary>
        /// <returns>This builder, so that several shared pieces are recorded in one expression.</returns>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public DescriptionBuilder Add(IReadOnlyList<AttributeDescription> attributes)
        {
            foreach (AttributeDescription attribute in attributes)
            {
                Record(attribute);
            }

            return this;
        }

        /// <summary>Records a slot another type owns and reads by, such as the OriginTarget child of a region.</summary>
        /// <returns>This builder, so that several shared pieces are recorded in one expression.</returns>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        public DescriptionBuilder Add(SlotDescription slot)
        {
            Record(slot);

            return this;
        }

        /// <summary>Seals what was recorded into the description, the attributes and slots in the order they were made.</summary>
        /// <remarks>Every call after this one is refused, so what a component hands out is the description its fields went into.</remarks>
        /// <exception cref="InvalidOperationException">The description has already been built.</exception>
        public ComponentDescription Build()
        {
            RefuseWhenBuilt();
            _isBuilt = true;

            if (_abstraction == null)
            {
                return new ComponentDescription(_summary, _attributes, _slots);
            }

            return new ComponentDescription(_abstraction.Value, _summary, _attributes, _slots);
        }

        /// <summary>Keeps an attribute in the order it was recorded and hands it back for the field that names it.</summary>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        private AttributeDescription Record(AttributeDescription attribute)
        {
            RefuseWhenBuilt();
            _attributes.Add(attribute);

            return attribute;
        }

        /// <summary>Keeps a slot in the order it was recorded and hands it back for the field that names it.</summary>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        private SlotDescription Record(SlotDescription slot)
        {
            RefuseWhenBuilt();
            _slots.Add(slot);

            return slot;
        }

        /// <summary>Refuses every call that would change or reissue a description already handed out.</summary>
        /// <exception cref="InvalidOperationException">The description has been built.</exception>
        private void RefuseWhenBuilt()
        {
            if (_isBuilt)
            {
                throw new InvalidOperationException(
                    $"The description of {_summary} has been built, so it takes nothing further and is built only once.");
            }
        }
    }
}
