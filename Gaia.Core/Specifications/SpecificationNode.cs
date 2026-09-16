using System.Collections.Generic;
using System.Globalization;
using Gaia.Core.Services.Log;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.Specifications
{
    /// <summary>
    ///     One element of a parsed configuration, holding its name, its type discriminator, its attributes, and its
    ///     children. Components read their parameters from SpecificationNodes, so they do not have to directly parse
    ///     configuration languages.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The typed accessors come in two tiers. A required attribute is read through ReadFloat and its siblings, which
    ///         report exactly what is missing or malformed and where. An optional attribute is read through TryGetFloat and
    ///         its siblings, which stay silent about an absent attribute so the caller can supply its own default. Malformed
    ///         text is an error through either tier, optional never means malformed text passes silently.
    ///     </para>
    ///     <para>
    ///         Every report carries the source name and line captured when the node was parsed, and is filed under the
    ///         source name as its channel, so one file's problems can be filtered from another's.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.3</seealso>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class SpecificationNode
    {
        private readonly List<AiString> _attributeNames = new();
        private readonly Dictionary<AiString, string> _attributes = new();
        private readonly List<SpecificationNode> _children = new();
        private readonly List<string> _leadingComments = new();
        private readonly ILogSink _logSink;
        private readonly List<string> _trailingComments = new();

        /// <summary>Creates an empty node. The reader fills it through SetAttribute and AddChild.</summary>
        internal SpecificationNode(
            AiString name,
            AiString type,
            string sourceDocumentName,
            int lineNumber,
            ILogSink logSink)
        {
            Name = name;
            Type = type;
            SourceDocumentName = sourceDocumentName;
            LineNumber = lineNumber;
            _logSink = logSink;
        }

        /// <summary>The element's name, such as Region or Option.</summary>
        public AiString Name { get; }

        /// <summary>
        ///     The Type attribute, which picks the component a factory builds. An element that groups children rather than
        ///     describing a component of its own carries no Type, and reports <see cref="AiString.Empty" /> here.
        /// </summary>
        public AiString Type { get; }

        /// <summary>The document this node came from, as it was named to the reader.</summary>
        public string SourceDocumentName { get; }

        /// <summary>The line the element started on in its document.</summary>
        public int LineNumber { get; }

        /// <summary>The child elements, in document order.</summary>
        public IReadOnlyList<SpecificationNode> Children => _children;

        /// <summary>The attribute names this element carries, in the order they were set.</summary>
        /// <remarks>Document order for a parsed tree, which is what lets the writer give back what was written.</remarks>
        internal IReadOnlyList<AiString> AttributeNames => _attributeNames;

        /// <summary>The comments that stood immediately above this element, in order, without their markers.</summary>
        /// <remarks>
        ///     Comments are designer documentation and no loader reads them, but the node carries them so that a tree written
        ///     back out keeps what its author wrote. The text is kept exactly as it appeared between the markers, surrounding
        ///     spaces included.
        /// </remarks>
        public IReadOnlyList<string> LeadingComments => _leadingComments;

        /// <summary>The comments that stood inside this element after its last child.</summary>
        public IReadOnlyList<string> TrailingComments => _trailingComments;

        /// <summary>Adds a child node. Called by the reader while building the tree.</summary>
        internal void AddChild(SpecificationNode child)
        {
            _children.Add(child);
        }

        /// <summary>Records a comment that stood above this element. Called by the reader while building the tree.</summary>
        internal void AddLeadingComment(string text)
        {
            _leadingComments.Add(text);
        }

        /// <summary>Records a comment that stood after this element's last child. Called by the reader.</summary>
        internal void AddTrailingComment(string text)
        {
            _trailingComments.Add(text);
        }

        /// <summary>The raw text of the named attribute, for the writer. The typed accessors are how loaders read.</summary>
        internal string GetAttributeText(AiString name)
        {
            return _attributes[name];
        }

        /// <summary>Stores an attribute's raw text. Called by the reader while building the tree.</summary>
        internal void SetAttribute(AiString name, string value)
        {
            if (!_attributes.ContainsKey(name))
            {
                _attributeNames.Add(name);
            }

            _attributes[name] = value;
        }

        /// <summary>Reports whether the element carries the named attribute.</summary>
        /// <remarks>
        ///     What a loader checks before reading an optional attribute whose default is not the accessor's, so that absence
        ///     keeps the default while malformed text still fails the read.
        /// </remarks>
        public bool HasAttribute(AiString name)
        {
            return _attributes.ContainsKey(name);
        }

        /// <summary>Reads an optional bool attribute. Absence is silent, malformed text is reported.</summary>
        public bool TryGetBool(AiString name, out bool value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseBool(name, text, out value);
        }

        /// <summary>Reads a required bool attribute. Absence and malformed text are both reported.</summary>
        public bool ReadBool(AiString name, out bool value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseBool(name, text, out value);
        }

        /// <summary>Reads an optional float attribute. Absence is silent, malformed text is reported.</summary>
        public bool TryGetFloat(AiString name, out float value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseFloat(name, text, out value);
        }

        /// <summary>Reads a required float attribute. Absence and malformed text are both reported.</summary>
        public bool ReadFloat(AiString name, out float value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseFloat(name, text, out value);
        }

        /// <summary>Reads an optional int attribute. Absence is silent, malformed text is reported.</summary>
        public bool TryGetInt(AiString name, out int value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseInt(name, text, out value);
        }

        /// <summary>Reads a required int attribute. Absence and malformed text are both reported.</summary>
        public bool ReadInt(AiString name, out int value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseInt(name, text, out value);
        }

        /// <summary>Reads an optional string attribute. Absence is silent. No text is malformed.</summary>
        public bool TryGetString(AiString name, out string value)
        {
            if (_attributes.TryGetValue(name, out string? text))
            {
                value = text;
                return true;
            }

            value = string.Empty;
            return false;
        }

        /// <summary>Reads a required string attribute. Absence is reported. No text is malformed.</summary>
        public bool ReadString(AiString name, out string value)
        {
            if (TryGetString(name, out value))
            {
                return true;
            }

            ReportMissing(name);
            return false;
        }

        /// <summary>Reads an optional point-in-a-plane attribute. Absence is silent, malformed text is reported.</summary>
        public bool TryGetVector2(AiString name, out Vector2 value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseVector2(name, text, out value);
        }

        /// <summary>Reads a required point-in-a-plane attribute. Absence and malformed text are both reported.</summary>
        public bool ReadVector2(AiString name, out Vector2 value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseVector2(name, text, out value);
        }

        /// <summary>Reads an optional position attribute. Absence is silent, malformed text is reported.</summary>
        public bool TryGetVector3(AiString name, out Vector3 value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseVector3(name, text, out value);
        }

        /// <summary>Reads a required position attribute. Absence and malformed text are both reported.</summary>
        public bool ReadVector3(AiString name, out Vector3 value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseVector3(name, text, out value);
        }

        /// <summary>Parses exactly "true" or "false", reporting anything else as malformed.</summary>
        private bool TryParseBool(AiString name, string text, out bool value)
        {
            switch (text)
            {
                case "true":
                    value = true;
                    return true;
                case "false":
                    value = false;
                    return true;
                default:
                    ReportMalformed(name, text, "true or false");
                    value = default;
                    return false;
            }
        }

        /// <summary>Parses a number in invariant culture, so a decimal mark is always a point.</summary>
        private bool TryParseFloat(AiString name, string text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            ReportMalformed(name, text, "a number");
            return false;
        }

        /// <summary>Parses a whole number in invariant culture.</summary>
        private bool TryParseInt(AiString name, string text, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            ReportMalformed(name, text, "a whole number");
            return false;
        }

        /// <summary>Parses the two-component tuple form, "(0, 1.5)", in invariant culture.</summary>
        private bool TryParseVector2(AiString name, string text, out Vector2 value)
        {
            string trimmed = text.Trim();

            if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')')
            {
                string[] parts = trimmed.Substring(1, trimmed.Length - 2).Split(',');

                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    value = new Vector2(x, y);
                    return true;
                }
            }

            ReportMalformed(name, text, "a tuple like (0, 1.5)");
            value = default;
            return false;
        }

        /// <summary>Parses the parenthesized tuple form, "(0, 1.5, 300)", with whitespace allowed around each part.</summary>
        private bool TryParseVector3(AiString name, string text, out Vector3 value)
        {
            string trimmed = text.Trim();

            if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')')
            {
                string[] parts = trimmed.Substring(1, trimmed.Length - 2).Split(',');

                if (parts.Length == 3 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    value = new Vector3(x, y, z);
                    return true;
                }
            }

            ReportMalformed(name, text, "a tuple like (0, 1.5, 300)");
            value = default;
            return false;
        }

        /// <summary>Reports a required attribute the element does not carry.</summary>
        private void ReportMissing(AiString name)
        {
            _logSink.LogError(SourceDocumentName,
                $"Line {LineNumber}, the {Name.Text} element has no {name.Text} attribute");
        }

        /// <summary>Reports two attributes on one element that say the same thing differently.</summary>
        /// <remarks>
        ///     Internal rather than private so that a loader can refuse a combination it cannot resolve, such as a veto and a
        ///     multiplier.
        /// </remarks>
        internal void ReportConflict(AiString name, AiString other)
        {
            _logSink.LogError(SourceDocumentName,
                $"Line {LineNumber}, the {Name.Text} element mixes {name.Text} with {other.Text}");
        }

        /// <summary>Reports an attribute whose text cannot be read as the kind that was asked for.</summary>
        /// <remarks>
        ///     Internal rather than private so that a loader can also refuse a value that parsed but has no meaning the
        ///     configuration could intend, such as a negative multiplier.
        /// </remarks>
        internal void ReportMalformed(AiString name, string text, string expected)
        {
            _logSink.LogError(SourceDocumentName,
                $"Line {LineNumber}, the {Name.Text} element's {name.Text} is not {expected}, it reads \"{text}\"");
        }
    }
}
