using System.Text;
using Gaia.Core.Strings;

namespace Gaia.Core.Specifications
{
    /// <summary>Writes a specification node tree back out as XML text in a canonical format.</summary>
    /// <remarks>
    ///     <para>
    ///         One canonical form is what keeps hand-authored and tool-authored files converging to the same bytes, so diffs
    ///         stay reviewable. Elements indent by four spaces per level, Type is always the first attribute, the remaining
    ///         attributes keep the order they were set in, an element without children closes itself, and every line ends with
    ///         a line feed alone, so a version control system that normalizes line endings converts them for each platform.
    ///     </para>
    ///     <para>
    ///         Comments write back where they stood, each above the element that carries it and trailing comments before
    ///         their element's closing tag, so a hand-commented file already in the canonical form survives a round trip
    ///         through the reader and this writer byte for byte.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.6.3</seealso>
    public sealed class XmlSpecificationWriter
    {
        private const string Indent = "    ";

        /// <summary>Writes the tree rooted at the given node as canonical XML text, ending with a newline.</summary>
        public string Write(SpecificationNode root)
        {
            StringBuilder builder = new();
            WriteNode(builder, root, 0);
            return builder.ToString();
        }

        /// <summary>Writes one node at the given depth, and its children one level deeper.</summary>
        private static void WriteNode(StringBuilder builder, SpecificationNode node, int depth)
        {
            foreach (string comment in node.LeadingComments)
            {
                AppendComment(builder, comment, depth);
            }

            AppendIndent(builder, depth);
            builder.Append('<').Append(node.Name.Text);

            if (!node.Type.IsEmpty)
            {
                builder.Append(" Type=\"").Append(Escape(node.Type.Text)).Append('"');
            }

            foreach (AiString name in node.AttributeNames)
            {
                builder.Append(' ').Append(name.Text).Append("=\"").Append(Escape(node.GetAttributeText(name)))
                    .Append('"');
            }

            if (node.Children.Count == 0 && node.TrailingComments.Count == 0)
            {
                builder.Append("/>\n");
                return;
            }

            builder.Append(">\n");

            foreach (SpecificationNode child in node.Children)
            {
                WriteNode(builder, child, depth + 1);
            }

            foreach (string comment in node.TrailingComments)
            {
                AppendComment(builder, comment, depth + 1);
            }

            AppendIndent(builder, depth);
            builder.Append("</").Append(node.Name.Text).Append(">\n");
        }

        /// <summary>Writes one comment on a line of its own at the given depth, its text exactly as the node holds it.</summary>
        private static void AppendComment(StringBuilder builder, string comment, int depth)
        {
            AppendIndent(builder, depth);
            builder.Append("<!--").Append(comment).Append("-->\n");
        }

        private static void AppendIndent(StringBuilder builder, int depth)
        {
            for (int level = 0; level < depth; level++)
            {
                builder.Append(Indent);
            }
        }

        /// <summary>Escapes the characters XML reserves, so any stored text survives the trip back through the reader.</summary>
        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
