using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Gaia.Core.Services.Log;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;

namespace Gaia.Core.Specifications
{
    /// <summary>
    ///     One element of a configuration, holding its name, its type discriminator, its attributes, and its children.
    ///     Components read their parameters from SpecificationNodes in <see cref="Factories.IAiComponent.Init" />, so they do
    ///     not have to directly parse configuration languages.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A node is parsed from a document by <see cref="XmlSpecificationReader" /> or built in code through the
    ///         constructor, and either kind is edited through the same members. Each member that changes the node throws
    ///         rather than hold what <see cref="XmlSpecificationWriter" /> could not write and the reader give back, which is
    ///         a name XML cannot carry, a character XML cannot carry, a name the element already carries, or a child that
    ///         another node holds. The XML parser refuses most of that before the reader meets it, and the reader reports the
    ///         rest, where a member would throw.
    ///     </para>
    ///     <para>
    ///         A name matches in any casing, since its identity is an <see cref="AiString" />, and the node keeps the
    ///         spelling it was given beside that identity. The writer writes the kept spelling, so what a tree writes depends
    ///         on the tree alone and not on which spelling <see cref="AiStringTable" /> registered first.
    ///     </para>
    ///     <para>
    ///         The typed accessors come in two tiers. A required attribute is read through ReadFloat and its siblings, which
    ///         report exactly what is missing or malformed and where. An optional attribute is read through TryGetFloat and
    ///         its siblings, which stay silent about an absent attribute so the caller can supply its own default. Malformed
    ///         text is an error through either tier, optional never means malformed text passes silently.
    ///     </para>
    ///     <para>
    ///         Every report carries the node's source name and line, and is filed under the source name as the channel of
    ///         <see cref="ILogSink.Write" />, so one file's problems can be filtered from another's.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.3</seealso>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class SpecificationNode
    {
        /// <summary>The name a document writes the type discriminator under, which no attribute may share.</summary>
        private static readonly AiString TypeAttribute = "Type";

        private readonly List<AiString> _attributeNames = new();
        private readonly Dictionary<AiString, string> _attributes = new();
        private readonly List<string> _attributeSpellings = new();
        private readonly List<SpecificationNode> _children = new();
        private readonly List<string> _leadingComments = new();
        private readonly ILogSink _logSink;
        private readonly List<string> _trailingComments = new();

        /// <summary>Creates a node with no attributes, children or comments, under the given element name and type.</summary>
        /// <remarks>
        ///     The name and the type are fixed from here on, so a component of another type is a new node put in this one's
        ///     place. Empty type text means the element carries no Type. A built node reports through a
        ///     <see cref="TraceLogSink" />, has no source name and is on line 0, so a host that wants a built tree's reports in
        ///     its own sink writes the tree and loads the text.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     The name is empty, holds a colon, is xmlns, or is otherwise not a name XML can
        ///     carry, or the type holds a character XML cannot carry.
        /// </exception>
        public SpecificationNode(string name, string type = "") : this(name, type, string.Empty, 0, new TraceLogSink())
        {
        }

        /// <summary>Creates a node for <see cref="XmlSpecificationReader" />, which knows the document, the line and the sink.</summary>
        /// <exception cref="ArgumentException">The name or the type is one the public constructor refuses.</exception>
        internal SpecificationNode(
            string name,
            string type,
            string sourceDocumentName,
            int lineNumber,
            ILogSink logSink)
        {
            string? nameProblem = NameProblem(name);

            if (nameProblem != null)
            {
                throw new ArgumentException($"The element name \"{name}\" {nameProblem}.", nameof(name));
            }

            string? typeProblem = TextProblem(type);

            if (typeProblem != null)
            {
                throw new ArgumentException($"The type {typeProblem}.", nameof(type));
            }

            Name = name;
            NameSpelling = name;
            Type = type;
            TypeSpelling = type;
            SourceDocumentName = sourceDocumentName;
            LineNumber = lineNumber;
            _logSink = logSink;
        }

        /// <summary>The element's name, such as Region or Option.</summary>
        public AiString Name { get; }

        /// <summary>The element's name as it was spelled where the node was parsed or built, which is what the writer writes.</summary>
        public string NameSpelling { get; }

        /// <summary>
        ///     The Type attribute, which picks the component a <see cref="Factories.ComponentFactory{TBase}" /> builds. An
        ///     element that groups children rather than describing a component of its own carries no Type, and reports
        ///     <see cref="AiString.Empty" /> here.
        /// </summary>
        public AiString Type { get; }

        /// <summary>The Type as it was spelled where the node was parsed or built, or empty text for an element with no Type.</summary>
        public string TypeSpelling { get; }

        /// <summary>The node whose <see cref="Children" /> hold this one, or null for a root and for a node not yet added.</summary>
        /// <remarks>Set by AddChild, InsertChild and RemoveChild and by no other member, so a node has one parent at most.</remarks>
        public SpecificationNode? Parent { get; private set; }

        /// <summary>The document this node came from, as it was named to the reader. Empty for a node built in code.</summary>
        public string SourceDocumentName { get; }

        /// <summary>The line the element started on in its document, or 0 for a node built in code.</summary>
        public int LineNumber { get; }

        /// <summary>The child elements, in document order.</summary>
        public IReadOnlyList<SpecificationNode> Children => _children;

        /// <summary>The names of the attributes this element carries, each as it was spelled when first set, in written order.</summary>
        /// <remarks>
        ///     Document order for a parsed tree. SetAttribute puts a name the element does not carry at the end, and
        ///     InsertAttribute puts it at an index. The Type is not among them.
        /// </remarks>
        public IReadOnlyList<string> AttributeSpellings => _attributeSpellings;

        /// <summary>The comments that stood immediately above this element, in order, without their markers.</summary>
        /// <remarks>
        ///     Comments are designer documentation and no loader reads them, but the node carries them so that a tree written
        ///     back out keeps what its author wrote. The text is kept exactly as it appeared between the markers, surrounding
        ///     spaces included.
        /// </remarks>
        public IReadOnlyList<string> LeadingComments => _leadingComments;

        /// <summary>The comments that stood inside this element after its last child.</summary>
        public IReadOnlyList<string> TrailingComments => _trailingComments;

        /// <summary>Adds a child node after the children the element already has.</summary>
        /// <exception cref="ArgumentException">
        ///     The child already has a <see cref="Parent" />, or is this node or one of its
        ///     ancestors.
        /// </exception>
        public void AddChild(SpecificationNode child)
        {
            InsertChild(_children.Count, child);
        }

        /// <summary>Adds a child node at the given index of <see cref="Children" />, moving the later children along by one.</summary>
        /// <remarks>
        ///     A node that already has a parent would be written once under each and read back as two nodes, and a node added
        ///     beneath itself would never finish writing, so both are refused. The second is found by walking up through
        ///     <see cref="Parent" /> from this node. RemoveChild reports the index that puts a removed child back where it stood.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The index is below 0 or above the number of children.</exception>
        /// <exception cref="ArgumentException">
        ///     The child already has a <see cref="Parent" />, or is this node or one of its
        ///     ancestors.
        /// </exception>
        public void InsertChild(int index, SpecificationNode child)
        {
            if (index < 0 || index > _children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"a child is inserted at an index from 0 to the {_children.Count} children the element has");
            }

            if (child.Parent != null)
            {
                throw new ArgumentException(
                    $"The {child.NameSpelling} node is already a child of a {child.Parent.NameSpelling} node. A node has one parent, so it is removed from that one first.",
                    nameof(child));
            }

            for (SpecificationNode? ancestor = this; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor == child)
                {
                    throw new ArgumentException(
                        $"The {child.NameSpelling} node is the node it is being added to, or one of that node's ancestors.",
                        nameof(child));
                }
            }

            _children.Insert(index, child);
            child.Parent = this;
        }

        /// <summary>Removes a child node, leaving it without a <see cref="Parent" />.</summary>
        /// <returns>
        ///     The index the child stood at, which InsertChild takes to put it back, or -1 when it is not a child of this
        ///     node.
        /// </returns>
        public int RemoveChild(SpecificationNode child)
        {
            int index = _children.IndexOf(child);

            if (index < 0)
            {
                return -1;
            }

            _children.RemoveAt(index);
            child.Parent = null;
            return index;
        }

        /// <summary>Sets an attribute's raw text, adding the attribute after the others when the element does not carry it.</summary>
        /// <remarks>
        ///     The name is matched in any casing. An attribute the element already carries keeps its place and the spelling
        ///     it was first given, and only its value changes.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     The name is one the constructor refuses or is Type in any casing, or the value
        ///     holds a character XML cannot carry.
        /// </exception>
        public void SetAttribute(string name, string value)
        {
            AiString identity = RequireAttributeName(name);
            RequireValue(value);

            if (!_attributes.ContainsKey(identity))
            {
                _attributeNames.Add(identity);
                _attributeSpellings.Add(name);
            }

            _attributes[identity] = value;
        }

        /// <summary>Adds an attribute the element does not carry at the given index of <see cref="AttributeSpellings" />.</summary>
        /// <remarks>What puts a removed attribute back where it stood, under the spelling it had.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">The index is below 0 or above the number of attributes.</exception>
        /// <exception cref="ArgumentException">
        ///     The name is one the constructor refuses, is Type in any casing, or is one the
        ///     element already carries in any casing, or the value holds a character XML cannot carry.
        /// </exception>
        public void InsertAttribute(int index, string name, string value)
        {
            if (index < 0 || index > _attributeNames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"an attribute is inserted at an index from 0 to the {_attributeNames.Count} attributes the element has");
            }

            AiString identity = RequireAttributeName(name);
            RequireValue(value);

            if (_attributes.ContainsKey(identity))
            {
                throw new ArgumentException(
                    $"The element already carries \"{name}\" as \"{_attributeSpellings[_attributeNames.IndexOf(identity)]}\". An element carries a name once, in whatever casing.",
                    nameof(name));
            }

            _attributeNames.Insert(index, identity);
            _attributeSpellings.Insert(index, name);
            _attributes[identity] = value;
        }

        /// <summary>Removes the named attribute, if the element carries it.</summary>
        /// <returns>True when the element carried the attribute.</returns>
        public bool RemoveAttribute(AiString name)
        {
            int index = _attributeNames.IndexOf(name);

            if (index < 0)
            {
                return false;
            }

            _attributeNames.RemoveAt(index);
            _attributeSpellings.RemoveAt(index);
            _attributes.Remove(name);
            return true;
        }

        /// <summary>Adds a comment below the ones that already stand above this element.</summary>
        /// <exception cref="ArgumentException">
        ///     The text holds two hyphens in a row, ends with a hyphen, or holds a carriage return
        ///     or another character an XML comment cannot carry.
        /// </exception>
        public void AddLeadingComment(string text)
        {
            RequireComment(text);
            _leadingComments.Add(text);
        }

        /// <summary>Adds a comment below the ones that already stand inside this element after its last child.</summary>
        /// <exception cref="ArgumentException">
        ///     The text holds two hyphens in a row, ends with a hyphen, or holds a carriage return
        ///     or another character an XML comment cannot carry.
        /// </exception>
        public void AddTrailingComment(string text)
        {
            RequireComment(text);
            _trailingComments.Add(text);
        }

        /// <summary>Removes every comment in <see cref="LeadingComments" />.</summary>
        public void ClearLeadingComments()
        {
            _leadingComments.Clear();
        }

        /// <summary>Removes every comment in <see cref="TrailingComments" />.</summary>
        public void ClearTrailingComments()
        {
            _trailingComments.Clear();
        }

        /// <summary>Reports the place of the named attribute in <see cref="AttributeSpellings" />.</summary>
        /// <returns>The index, or -1 when the element does not carry the attribute.</returns>
        public int IndexOfAttribute(AiString name)
        {
            return _attributeNames.IndexOf(name);
        }

        /// <summary>
        ///     The raw text of the attribute at the given index of <see cref="AttributeSpellings" />, for
        ///     <see cref="XmlSpecificationWriter" />. A component's Init reads through the typed accessors.
        /// </summary>
        internal string AttributeTextAt(int index)
        {
            return _attributes[_attributeNames[index]];
        }

        /// <summary>Reports whether the element carries the named attribute.</summary>
        /// <remarks>
        ///     What a component's Init checks before reading an optional attribute whose default is not the zero value
        ///     TryGetFloat and its siblings leave in their out parameter, so that absence keeps the default while malformed text
        ///     still fails the read.
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

        /// <summary>
        ///     Reads an optional orientation attribute, written as pitch, yaw and roll in degrees. Absence is silent,
        ///     malformed text is reported.
        /// </summary>
        public bool TryGetRotation(AiString name, out Rotation value)
        {
            value = default;
            return _attributes.TryGetValue(name, out string? text) && TryParseRotation(name, text, out value);
        }

        /// <summary>
        ///     Reads a required orientation attribute, written as pitch, yaw and roll in degrees. Absence and malformed text
        ///     are both reported.
        /// </summary>
        public bool ReadRotation(AiString name, out Rotation value)
        {
            value = default;

            if (!_attributes.TryGetValue(name, out string? text))
            {
                ReportMissing(name);
                return false;
            }

            return TryParseRotation(name, text, out value);
        }

        /// <summary>Parses "true" or "false" in any casing, reporting anything else as malformed.</summary>
        private bool TryParseBool(AiString name, string text, out bool value)
        {
            // Names match case-insensitively everywhere, so a value parsed more strictly than its own attribute name is
            // matched would turn the chapter's Veto="True" into a malformed-value error.
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            ReportMalformed(name, text, "true or false");
            value = default;
            return false;
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

        /// <summary>Parses the two-component tuple form, "(0, 1.5)".</summary>
        private bool TryParseVector2(AiString name, string text, out Vector2 value)
        {
            float[] parts = new float[2];

            if (TryParseTuple(name, text, "a tuple like (0, 1.5)", parts))
            {
                value = new Vector2(parts[0], parts[1]);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Parses the three-component tuple form, "(0, 1.5, 300)".</summary>
        private bool TryParseVector3(AiString name, string text, out Vector3 value)
        {
            float[] parts = new float[3];

            if (TryParseTuple(name, text, "a tuple like (0, 1.5, 300)", parts))
            {
                value = new Vector3(parts[0], parts[1], parts[2]);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Parses pitch, yaw and roll in degrees, written in the tuple form, "(0, 45, 0)".</summary>
        private bool TryParseRotation(AiString name, string text, out Rotation value)
        {
            float[] parts = new float[3];

            if (TryParseTuple(name, text, "pitch, yaw and roll in degrees like (0, 45, 0)", parts))
            {
                value = Rotation.FromEulerAngles(parts[0], parts[1], parts[2]);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Parses the parenthesized tuple form into as many numbers as parts has room for.</summary>
        /// <remarks>
        ///     Whitespace is allowed around each part, and each is read in invariant culture, as TryParseFloat reads one.
        ///     Anything else, a different count of parts included, is reported as not being what expected describes.
        /// </remarks>
        private bool TryParseTuple(AiString name, string text, string expected, float[] parts)
        {
            string trimmed = text.Trim();

            if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')')
            {
                string[] texts = trimmed.Substring(1, trimmed.Length - 2).Split(',');

                if (texts.Length == parts.Length)
                {
                    int parsed = 0;

                    while (parsed < parts.Length && float.TryParse(texts[parsed], NumberStyles.Float,
                               CultureInfo.InvariantCulture, out parts[parsed]))
                    {
                        parsed++;
                    }

                    if (parsed == parts.Length)
                    {
                        return true;
                    }
                }
            }

            ReportMalformed(name, text, expected);
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
        ///     Internal rather than private so that a component's Init can refuse a combination it cannot resolve, such as a
        ///     veto and a multiplier.
        /// </remarks>
        internal void ReportConflict(AiString name, AiString other)
        {
            _logSink.LogError(SourceDocumentName,
                $"Line {LineNumber}, the {Name.Text} element mixes {name.Text} with {other.Text}");
        }

        /// <summary>Reports an attribute whose text cannot be read as the kind that was asked for.</summary>
        /// <remarks>
        ///     Internal rather than private so that a component's Init can also refuse a value that parsed but has no meaning
        ///     the configuration could intend, such as a negative multiplier.
        /// </remarks>
        internal void ReportMalformed(AiString name, string text, string expected)
        {
            _logSink.LogError(SourceDocumentName,
                $"Line {LineNumber}, the {Name.Text} element's {name.Text} is not {expected}, it reads \"{text}\"");
        }

        /// <summary>Refuses a name no attribute may have, and converts one that passes to its identity.</summary>
        /// <exception cref="ArgumentException">NameProblem finds a problem with the name, or the name is Type in any casing.</exception>
        private static AiString RequireAttributeName(string name)
        {
            string? problem = NameProblem(name);

            if (problem != null)
            {
                throw new ArgumentException($"The attribute name \"{name}\" {problem}.", nameof(name));
            }

            if (IsTypeName(name))
            {
                throw new ArgumentException(
                    $"The attribute name \"{name}\" is the Type, which is fixed when the node is built and is not an attribute.",
                    nameof(name));
            }

            return name;
        }

        /// <summary>Refuses an attribute value XML cannot carry.</summary>
        /// <exception cref="ArgumentException">TextProblem finds a problem with the value.</exception>
        private static void RequireValue(string value)
        {
            string? problem = TextProblem(value);

            if (problem != null)
            {
                throw new ArgumentException($"The value {problem}.", nameof(value));
            }
        }

        /// <summary>Refuses a comment an XML comment cannot carry.</summary>
        /// <exception cref="ArgumentException">CommentProblem finds a problem with the text.</exception>
        private static void RequireComment(string text)
        {
            string? problem = CommentProblem(text);

            if (problem != null)
            {
                throw new ArgumentException($"The comment {problem}.", nameof(text));
            }
        }

        /// <summary>Reports whether a name is the Type's, in any casing, which makes it the discriminator and never an attribute.</summary>
        internal static bool IsTypeName(string name)
        {
            AiString identity = name;
            return identity == TypeAttribute;
        }

        /// <summary>Asks whether an element or attribute name is one the writer can write and the reader give back.</summary>
        /// <remarks>
        ///     The answer depends on the spelling given and on nothing <see cref="AiStringTable" /> holds, since two
        ///     spellings of one name can differ in whether XML takes them. A colon and the name xmlns are refused because either
        ///     one makes the written text use an XML namespace, which the reader refuses. XMLNS in any other casing is an ordinary
        ///     name to XML.
        /// </remarks>
        /// <returns>Null for a name that passes. Otherwise the rest of a sentence whose subject is the name, saying what is wrong.</returns>
        internal static string? NameProblem(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "is empty";
            }

            if (name.IndexOf(':') >= 0)
            {
                return "holds a colon, which XML reads as a namespace prefix, and Gaia reads no XML namespace";
            }

            if (name == "xmlns")
            {
                return "is what XML declares a namespace with, and Gaia reads no XML namespace";
            }

            bool isName = XmlConvert.IsStartNCNameChar(name[0]);

            for (int index = 1; isName && index < name.Length; index++)
            {
                isName = XmlConvert.IsNCNameChar(name[index]);
            }

            return isName ? null : "is not a name XML can carry";
        }

        /// <summary>Asks whether an attribute value or a Type holds only characters XML can carry.</summary>
        /// <returns>
        ///     Null for text that passes. Otherwise the rest of a sentence whose subject is the text, naming the first
        ///     character that is wrong.
        /// </returns>
        private static string? TextProblem(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];

                if (XmlConvert.IsXmlChar(character))
                {
                    continue;
                }

                // A character numbered above U+FFFF is two chars, which XML carries only as the high half followed by
                // the low half.
                if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], character))
                {
                    index++;
                    continue;
                }

                return FormattableString.Invariant($"holds U+{(int)character:X4}, a character XML cannot carry");
            }

            return null;
        }

        /// <summary>Asks whether a comment's text is text an XML comment can carry and the reader give back unchanged.</summary>
        /// <remarks>
        ///     XML ends a comment at two hyphens, so a comment holds no two in a row and none at its end, where the closing
        ///     marker would complete the pair. A parser turns a carriage return into a line feed, so a comment that held one would
        ///     read back as different text.
        /// </remarks>
        /// <returns>
        ///     Null for text that passes. Otherwise the rest of a sentence whose subject is the comment, saying what is
        ///     wrong.
        /// </returns>
        private static string? CommentProblem(string text)
        {
            string? problem = TextProblem(text);

            if (problem != null)
            {
                return problem;
            }

            if (text.IndexOf("--", StringComparison.Ordinal) >= 0)
            {
                return "holds two hyphens in a row, which XML allows only where a comment ends";
            }

            if (text.EndsWith("-", StringComparison.Ordinal))
            {
                return "ends with a hyphen, which would join the closing marker's two";
            }

            if (text.IndexOf('\r') >= 0)
            {
                return "holds a carriage return, which a parser turns into a line feed";
            }

            return null;
        }
    }
}
