using System;
using System.Collections.Generic;
using Gaia.Core.DataStores;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>One attribute a component reads from its element, described for a tool that lists what the element may hold.</summary>
    /// <remarks>
    ///     The name is the same <see cref="AiString" /> the component's Init reads by, and the spelling is the text a
    ///     tool writes the attribute under, since an AiString keeps no spelling of its own. A required attribute carries no
    ///     default. An optional attribute's default is the text the reader takes an absent attribute as, or null where absence
    ///     means no value at all.
    /// </remarks>
    public sealed class AttributeDescription
    {
        /// <summary>
        ///     Keeps what the five static builders share, the check on the spelling. It stores what it is given and checks
        ///     nothing about the kind against the other arguments.
        /// </summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        private AttributeDescription(
            AiString name,
            string spelling,
            AttributeKind kind,
            bool isRequired,
            string? defaultText,
            string summary,
            IReadOnlyList<string>? choices = null,
            DataStoreValueType? variableKind = null)
        {
            AiString spelled = spelling;

            if (spelled != name)
            {
                throw new ArgumentException(
                    $"The spelling \"{spelling}\" does not spell the attribute name {name.Text}.", nameof(spelling));
            }

            Name = name;
            Spelling = spelling;
            Kind = kind;
            IsRequired = isRequired;
            DefaultText = defaultText;
            Summary = summary;
            Choices = choices == null ? null : new List<string>(choices).AsReadOnly();
            VariableKind = variableKind;
        }

        /// <summary>The name the component reads the attribute by.</summary>
        public AiString Name { get; }

        /// <summary>The name as a document writes it.</summary>
        public string Spelling { get; }

        /// <summary>What the attribute is read as.</summary>
        public AttributeKind Kind { get; }

        /// <summary>True when the component fails to load without the attribute.</summary>
        public bool IsRequired { get; }

        /// <summary>
        ///     The text an absent attribute reads as, or null for a required attribute and for one whose absence means no
        ///     value.
        /// </summary>
        public string? DefaultText { get; }

        /// <summary>One line saying what the attribute does, in a designer's words.</summary>
        public string Summary { get; }

        /// <summary>The names an Enumeration attribute may hold, or null for every other kind.</summary>
        public IReadOnlyList<string>? Choices { get; }

        /// <summary>The kind of stored value a VariableName attribute is read as, or null for every other kind.</summary>
        public DataStoreValueType? VariableKind { get; }

        /// <summary>The same attribute described with another default, for an element that reads it over other identity values.</summary>
        /// <remarks>Public so that a game's component can describe a shared attribute under a default of its own.</remarks>
        public AttributeDescription WithDefault(string? defaultText)
        {
            return new AttributeDescription(Name, Spelling, Kind, IsRequired, defaultText, Summary, Choices,
                VariableKind);
        }

        /// <summary>
        ///     Describes a required attribute of the given kind. An Enumeration and a VariableName have builders of their own
        ///     that carry what those kinds need, and this one does not check the kind.
        /// </summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public static AttributeDescription Required(AiString name, string spelling, AttributeKind kind, string summary)
        {
            return new AttributeDescription(name, spelling, kind, true, null, summary);
        }

        /// <summary>
        ///     Describes an optional attribute of the given kind, with its default as text or null. An Enumeration and a
        ///     VariableName have builders of their own that carry what those kinds need, and this one does not check the kind.
        /// </summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public static AttributeDescription Optional(
            AiString name,
            string spelling,
            AttributeKind kind,
            string? defaultText,
            string summary)
        {
            return new AttributeDescription(name, spelling, kind, false, defaultText, summary);
        }

        /// <summary>Describes an optional attribute read as one of the enumeration's names, taking the choices from the type.</summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public static AttributeDescription Enumeration<TEnum>(
            AiString name,
            string spelling,
            string defaultText,
            string summary) where TEnum : struct, Enum
        {
            return new AttributeDescription(name, spelling, AttributeKind.Enumeration, false, defaultText, summary,
                Enum.GetNames(typeof(TEnum)));
        }

        /// <summary>Describes an optional attribute read as one of the given names, for choices no enumeration type lists.</summary>
        /// <remarks>
        ///     The choices are kept in the order given. The default is not checked against them, so the caller passes one of
        ///     the choices.
        /// </remarks>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public static AttributeDescription Enumeration(
            AiString name,
            string spelling,
            IReadOnlyList<string> choices,
            string defaultText,
            string summary)
        {
            return new AttributeDescription(name, spelling, AttributeKind.Enumeration, false, defaultText, summary,
                choices);
        }

        /// <summary>Describes a required attribute naming a stored value that is read as the given kind.</summary>
        /// <exception cref="ArgumentException">The spelling does not spell the name.</exception>
        public static AttributeDescription VariableName(
            AiString name,
            string spelling,
            DataStoreValueType variableKind,
            string summary)
        {
            return new AttributeDescription(name, spelling, AttributeKind.VariableName, true, null, summary, null,
                variableKind);
        }
    }
}
