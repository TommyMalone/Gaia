using System.Xml;
using System.Xml.Linq;
using Gaia.Core.Services.Log;
using Gaia.Core.Strings;

namespace Gaia.Core.Specifications
{
    /// <summary>
    ///     Builds <see cref="SpecificationNode" /> trees from XML text, the one place in the assembly that reads a
    ///     concrete format.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each element becomes a node carrying the element's name, its Type attribute as the discriminator, the
    ///         remaining attributes as raw text, and its child elements in order. Comments vanish in parsing. Non whitespace
    ///         text inside an element is reported as a warning naming the element, since a configuration is attribute shaped
    ///         and stray text usually indicates a mistake, but it does not fail the read.
    ///     </para>
    ///     <para>
    ///         The reader takes text rather than a file, so whatever owns files does its own reading and passes the name it
    ///         wants errors filed under. Malformed input is reported as one error through <see cref="LogSink" /> and yields no
    ///         tree rather than an exception.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class XmlSpecificationReader
    {
        /// <summary>The name of the attribute that picks the component a factory builds.</summary>
        private const string TypeAttributeName = "Type";

        /// <summary>Where the reader and every node it builds report their errors.</summary>
        public ILogSink LogSink { get; internal set; } = new TraceLogSink();

        /// <summary>Parses one document into a node tree.</summary>
        /// <returns>True when the document parsed. False after reporting one error filed under the source name.</returns>
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
                LogSink.LogError(sourceDocumentName,
                    $"Line {exception.LineNumber}, the document could not be parsed. {exception.Message}");
                return false;
            }

            if (document.Root == null)
            {
                LogSink.LogError(sourceDocumentName, "The document has no root element");
                return false;
            }

            root = BuildNode(document.Root, sourceDocumentName);
            return true;
        }

        /// <summary>Builds the node for one element and recurses into its children.</summary>
        private SpecificationNode BuildNode(XElement element, string sourceDocumentName)
        {
            XAttribute? typeAttribute = element.Attribute(TypeAttributeName);
            AiString type = typeAttribute?.Value ?? AiString.Empty;

            SpecificationNode node = new(element.Name.LocalName, type, sourceDocumentName, LineOf(element), LogSink);

            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute != typeAttribute)
                {
                    node.SetAttribute(attribute.Name.LocalName, attribute.Value);
                }
            }

            foreach (XNode child in element.Nodes())
            {
                if (child is XElement childElement)
                {
                    node.AddChild(BuildNode(childElement, sourceDocumentName));
                }
                else if (child is XText childText && childText.Value.Trim().Length > 0)
                {
                    LogSink.LogWarning(sourceDocumentName,
                        $"Line {LineOf(childText)}, the {element.Name.LocalName} element holds stray text " +
                        $"\"{childText.Value.Trim()}\"");
                }
            }

            return node;
        }

        /// <summary>The line a parsed piece of the document started on, or 0 when the parser kept no line info.</summary>
        private static int LineOf(IXmlLineInfo lineInfo)
        {
            return lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
        }
    }
}
