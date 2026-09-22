using System.Collections.Generic;
using System.Text;

namespace Gaia.Core.Specifications
{
    /// <summary>Writes a specification node tree back out as XML text in a canonical format.</summary>
    /// <remarks>
    ///     <para>
    ///         One canonical form is what keeps hand-authored and tool-authored files converging to the same bytes, so diffs
    ///         stay reviewable. Elements indent by four spaces per level, Type is always the first attribute and is always
    ///         written as Type, the remaining attributes keep the order they were set in, an element without children closes
    ///         itself, and every line ends with a line feed alone, so a version control system that normalizes line endings
    ///         converts them for each platform.
    ///     </para>
    ///     <para>
    ///         Every name and the Type's value are written in the spelling the node keeps, so the text is a function of the
    ///         tree, and two documents that spell one name differently each write their own spelling in one process.
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
            int lineCount = 0;
            WriteNode(builder, root, 0, null, ref lineCount);
            return builder.ToString();
        }

        /// <summary>Writes the tree as the overload taking the root alone does, and reports where each node was written.</summary>
        /// <param name="root">The node the written document's root element describes.</param>
        /// <param name="lineByNode">
        ///     For every node of the tree, the line of the returned text its opening tag is on, counted from
        ///     1 as <see cref="SpecificationNode.LineNumber" /> is, which is the line a report names when the text is read back.
        /// </param>
        public string Write(SpecificationNode root, out IReadOnlyDictionary<SpecificationNode, int> lineByNode)
        {
            StringBuilder builder = new();
            Dictionary<SpecificationNode, int> lines = new();
            int lineCount = 0;
            WriteNode(builder, root, 0, lines, ref lineCount);
            lineByNode = lines;
            return builder.ToString();
        }

        /// <summary>Writes one node at the given depth, and its children one level deeper.</summary>
        /// <param name="builder">The text written so far.</param>
        /// <param name="node">The node to write.</param>
        /// <param name="depth">How many levels the node is indented by.</param>
        /// <param name="lineByNode">Where each node's line is recorded, or null when the caller did not ask for the lines.</param>
        /// <param name="lineCount">How many line feeds the builder holds, which every append of one keeps in step.</param>
        private static void WriteNode(
            StringBuilder builder,
            SpecificationNode node,
            int depth,
            Dictionary<SpecificationNode, int>? lineByNode,
            ref int lineCount)
        {
            foreach (string comment in node.LeadingComments)
            {
                AppendComment(builder, comment, depth, ref lineCount);
            }

            if (lineByNode != null)
            {
                lineByNode[node] = lineCount + 1;
            }

            AppendIndent(builder, depth);
            builder.Append('<').Append(node.NameSpelling);

            if (node.TypeSpelling.Length > 0)
            {
                builder.Append(" Type=\"");
                AppendEscaped(builder, node.TypeSpelling);
                builder.Append('"');
            }

            for (int index = 0; index < node.AttributeSpellings.Count; index++)
            {
                builder.Append(' ').Append(node.AttributeSpellings[index]).Append("=\"");
                AppendEscaped(builder, node.AttributeTextAt(index));
                builder.Append('"');
            }

            if (node.Children.Count == 0 && node.TrailingComments.Count == 0)
            {
                builder.Append("/>\n");
                lineCount++;
                return;
            }

            builder.Append(">\n");
            lineCount++;

            foreach (SpecificationNode child in node.Children)
            {
                WriteNode(builder, child, depth + 1, lineByNode, ref lineCount);
            }

            foreach (string comment in node.TrailingComments)
            {
                AppendComment(builder, comment, depth + 1, ref lineCount);
            }

            AppendIndent(builder, depth);
            builder.Append("</").Append(node.NameSpelling).Append(">\n");
            lineCount++;
        }

        /// <summary>Writes one comment at the given depth, its text exactly as the node holds it, and ends the line after it.</summary>
        /// <remarks>A comment may hold line feeds of its own, which are counted so the lines reported below it stay right.</remarks>
        private static void AppendComment(StringBuilder builder, string comment, int depth, ref int lineCount)
        {
            AppendIndent(builder, depth);
            builder.Append("<!--").Append(comment).Append("-->\n");
            lineCount++;

            foreach (char character in comment)
            {
                if (character == '\n')
                {
                    lineCount++;
                }
            }
        }

        private static void AppendIndent(StringBuilder builder, int depth)
        {
            for (int level = 0; level < depth; level++)
            {
                builder.Append(Indent);
            }
        }

        /// <summary>Writes an attribute's text so that the reader gives back exactly what the node holds.</summary>
        /// <remarks>
        ///     The characters XML reserves are written as entities. A tab, a line feed and a carriage return are written as
        ///     character references, because a parser turns each of them into a space when it stands in an attribute as itself.
        /// </remarks>
        private static void AppendEscaped(StringBuilder builder, string text)
        {
            foreach (char character in text)
            {
                switch (character)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    case '\t':
                        builder.Append("&#x9;");
                        break;
                    case '\n':
                        builder.Append("&#xA;");
                        break;
                    case '\r':
                        builder.Append("&#xD;");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }
        }
    }
}
