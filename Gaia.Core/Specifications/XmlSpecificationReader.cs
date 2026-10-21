using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Gaia.Core.Services.Log;

namespace Gaia.Core.Specifications
{
    /// <summary>
    ///     Builds <see cref="SpecificationNode" /> trees from XML text, the one place in the assembly that reads a
    ///     concrete format.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The Type attribute is taken in any casing, as every other name is matched. Comments become the
    ///         <see cref="SpecificationNode.LeadingComments" /> of the element each stands above, or the
    ///         <see cref="SpecificationNode.TrailingComments" /> of the element it stands inside when no element follows it,
    ///         except a comment after the root's closing tag, which has no element to attach to and is reported as a warning.
    ///         Non whitespace text inside an element is reported as a warning naming the element, since a configuration is
    ///         attribute shaped and stray text usually indicates a mistake, but it does not fail the read.
    ///     </para>
    ///     <para>
    ///         The reader takes text rather than a file, so whatever owns files does its own reading and passes the name it
    ///         wants errors filed under. Text that is not well formed XML is reported as one error through
    ///         <see cref="LogSink" /> and yields no tree rather than an exception. A document that declares a document type is
    ///         refused as well, at the declaration's line, since a definition can declare an attribute default the text never
    ///         writes.
    ///     </para>
    ///     <para>
    ///         A well formed document can still describe a tree a node refuses to hold, and the reader reports each refusal
    ///         it meets as an error and reads on, the children of a refused element included. An element that carries Type or
    ///         any other name in two casings is refused, since a node matches names in any casing, and an element refused for
    ///         a namespace, for its name or for a second Type has its other attributes left unread, so a name among them
    ///         carried twice is reported by the read after that refusal is fixed. A namespace declaration, an element in a
    ///         namespace other than the default one, and an attribute in a namespace are refused, because Gaia reads no XML
    ///         namespace and dropping the prefix would take one name for another.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class XmlSpecificationReader
    {
        /// <summary>Creates a reader reporting to the given sink, or to a <see cref="TraceLogSink" /> when given none.</summary>
        public XmlSpecificationReader(ILogSink? logSink = null)
        {
            LogSink = logSink ?? new TraceLogSink();
        }

        /// <summary>Where the reader and every node it builds report their errors.</summary>
        public ILogSink LogSink { get; }

        /// <summary>Parses one document into a node tree.</summary>
        /// <returns>True when the document parsed. False after reporting every error found, filed under the source name.</returns>
        public bool TryRead(string text, string sourceDocumentName, out SpecificationNode? root)
        {
            root = null;

            XDocument document;

            try
            {
                document = XDocument.Parse(text, LoadOptions.SetLineInfo);
            }
            catch (XmlException exception)
            {
                // The parser reports text with no root element at line 0, a line no file has, so a report at line 0
                // leaves the line out.
                string report = exception.LineNumber > 0
                    ? $"Line {exception.LineNumber}, the document could not be parsed. {exception.Message}"
                    : $"The document could not be parsed. {exception.Message}";
                LogSink.LogError(sourceDocumentName, report);
                return false;
            }

            // XDocument.Parse reads a document type definition and applies its defaults. An XmlReader set to prohibit
            // one throws with no line number instead, so the parse is allowed to finish and the declaration is refused
            // here, at its own line, before anything it declared is read.
            if (document.DocumentType != null)
            {
                LogSink.LogError(sourceDocumentName,
                    $"Line {LineOf(document.DocumentType)}, the document declares a document type, and Gaia reads no document type definition");
                return false;
            }

            try
            {
                // XDocument.Parse throws for text with no root element, so a document that parsed has one.
                SpecificationNode? builtRoot = BuildNode(document.Root!, sourceDocumentName);

                if (builtRoot == null)
                {
                    return false;
                }

                AttachDocumentComments(document, builtRoot, sourceDocumentName);
                root = builtRoot;
                return true;
            }
            catch (ArgumentException exception)
            {
                // The parser has already refused most of what a node's members refuse, and BuildNode reports an element
                // named xmlns. Anything else a member throws is reported here rather than let out.
                LogSink.LogError(sourceDocumentName, $"The document could not be built. {exception.Message}");
                return false;
            }
        }

        /// <summary>Builds the node for one element and recurses into its children.</summary>
        /// <returns>
        ///     Null when the element or anything beneath it was refused. The children of a refused element are still visited,
        ///     so what is refused beneath it is reported in the same read.
        /// </returns>
        private SpecificationNode? BuildNode(XElement element, string sourceDocumentName)
        {
            int line = LineOf(element);
            string elementName = element.Name.LocalName;
            string type = string.Empty;
            string? typeName = null;
            bool isValid = true;

            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    isValid = false;
                    LogSink.LogError(sourceDocumentName,
                        $"Line {line}, the {elementName} element declares the XML namespace \"{attribute.Value}\", and Gaia reads no XML namespace");
                }
                else if (attribute.Name.Namespace != XNamespace.None)
                {
                    isValid = false;
                    LogSink.LogError(sourceDocumentName,
                        $"Line {line}, the {elementName} element's {attribute.Name.LocalName} is in the XML namespace \"{attribute.Name.NamespaceName}\", and Gaia reads no XML namespace");
                }
                else if (SpecificationNode.IsTypeName(attribute.Name.LocalName))
                {
                    if (typeName != null)
                    {
                        isValid = false;
                        LogSink.LogError(sourceDocumentName,
                            $"Line {line}, the {elementName} element carries {typeName} twice, the second time as {attribute.Name.LocalName}");
                    }
                    else
                    {
                        typeName = attribute.Name.LocalName;
                        type = attribute.Value;
                    }
                }
            }

            // An element with no prefix is in the default namespace, so one that is anywhere else was written with a
            // prefix. A default namespace other than none was reported where it was declared.
            if (element.Name.Namespace != element.GetDefaultNamespace())
            {
                isValid = false;
                LogSink.LogError(sourceDocumentName,
                    $"Line {line}, the {elementName} element is in the XML namespace \"{element.Name.NamespaceName}\", and Gaia reads no XML namespace");
            }
            else
            {
                // xmlns is a name XML lets an element take and a node refuses, so it is reported here.
                string? nameProblem = SpecificationNode.NameProblem(elementName);

                if (nameProblem != null)
                {
                    isValid = false;
                    LogSink.LogError(sourceDocumentName,
                        $"Line {line}, the {elementName} element's name {nameProblem}");
                }
            }

            SpecificationNode? node = null;

            if (isValid)
            {
                node = new SpecificationNode(elementName, type, sourceDocumentName, line, LogSink);
                isValid = AddAttributes(element, node);
            }

            // Comments gather here until the element they stand above arrives. Whatever is left at the end stood
            // after the last child and belongs to this element.
            List<string>? pendingComments = null;

            foreach (XNode child in element.Nodes())
            {
                if (child is XElement childElement)
                {
                    SpecificationNode? childNode = BuildNode(childElement, sourceDocumentName);

                    if (childNode == null || node == null)
                    {
                        isValid = false;
                        pendingComments?.Clear();
                        continue;
                    }

                    if (pendingComments != null)
                    {
                        foreach (string comment in pendingComments)
                        {
                            childNode.AddLeadingComment(comment);
                        }

                        pendingComments.Clear();
                    }

                    node.AddChild(childNode);
                }
                else if (child is XComment childComment)
                {
                    pendingComments ??= new List<string>();
                    pendingComments.Add(childComment.Value);
                }
                else if (child is XText childText && childText.Value.Trim().Length > 0)
                {
                    LogSink.LogWarning(sourceDocumentName,
                        $"Line {LineOf(childText)}, the {elementName} element holds stray text \"{childText.Value.Trim()}\"");
                }
            }

            if (!isValid || node == null)
            {
                return null;
            }

            if (pendingComments != null)
            {
                foreach (string comment in pendingComments)
                {
                    node.AddTrailingComment(comment);
                }
            }

            return node;
        }

        /// <summary>Gives a node each attribute of its element other than the Type.</summary>
        /// <remarks>
        ///     BuildNode calls this only for an element on which it found no namespace declaration and no attribute in a
        ///     namespace, so every attribute met here is the Type or an attribute for the node.
        /// </remarks>
        /// <returns>False when the element carries a name twice, after reporting each name that it does.</returns>
        private bool AddAttributes(XElement element, SpecificationNode node)
        {
            bool isValid = true;

            foreach (XAttribute attribute in element.Attributes())
            {
                string name = attribute.Name.LocalName;

                if (SpecificationNode.IsTypeName(name))
                {
                    continue;
                }

                int existingIndex = node.IndexOfAttribute(name);

                if (existingIndex >= 0)
                {
                    isValid = false;
                    LogSink.LogError(node.SourceDocumentName,
                        $"Line {node.LineNumber}, the {node.NameSpelling} element carries {node.AttributeSpellings[existingIndex]} twice, the second time as {name}");
                }
                else
                {
                    node.SetAttribute(name, attribute.Value);
                }
            }

            return isValid;
        }

        /// <summary>Attaches the comments that stood outside the root element, above it, to the root.</summary>
        private void AttachDocumentComments(XDocument document, SpecificationNode root, string sourceDocumentName)
        {
            bool rootSeen = false;

            foreach (XNode node in document.Nodes())
            {
                if (node is XElement)
                {
                    rootSeen = true;
                }
                else if (node is XComment comment)
                {
                    if (rootSeen)
                    {
                        LogSink.LogWarning(sourceDocumentName,
                            $"Line {LineOf(comment)}, the comment after the root element is not kept");
                    }
                    else
                    {
                        root.AddLeadingComment(comment.Value);
                    }
                }
            }
        }

        /// <summary>The line a parsed piece of the document started on, or 0 when the parser kept no line info.</summary>
        private static int LineOf(IXmlLineInfo lineInfo)
        {
            return lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
        }
    }
}
