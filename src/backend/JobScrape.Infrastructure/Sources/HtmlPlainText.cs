using System.Text;
using AngleSharp.Dom;

namespace JobScrape.Infrastructure.Sources
{
    /// <summary>
    /// Readable plain-text extraction shared by the source HTML helpers:
    /// block elements and <c>&lt;br&gt;</c> start new lines, list items get a
    /// <c>- </c> bullet, scripts and styles are dropped, entities are decoded
    /// by the parser, whitespace inside a line collapses, and blank lines are
    /// removed. Iterative, so hostile nesting depth cannot overflow the stack.
    /// </summary>
    internal static class HtmlPlainText
    {
        private static readonly HashSet<string> NonTextElements = new(StringComparer.Ordinal)
        {
            "script",
            "style",
            "noscript",
            "template",
            "head",
            "svg",
            "iframe",
            "button",
            "select",
        };

        private static readonly HashSet<string> BlockElements = new(StringComparer.Ordinal)
        {
            "address",
            "article",
            "aside",
            "blockquote",
            "dd",
            "div",
            "dl",
            "dt",
            "fieldset",
            "figcaption",
            "figure",
            "footer",
            "form",
            "h1",
            "h2",
            "h3",
            "h4",
            "h5",
            "h6",
            "header",
            "hr",
            "li",
            "main",
            "nav",
            "ol",
            "p",
            "pre",
            "section",
            "table",
            "tbody",
            "td",
            "th",
            "thead",
            "tr",
            "ul",
        };

        /// <summary>
        /// Converts an element's content to readable plain text.
        /// </summary>
        /// <param name="root">The element whose content is converted.</param>
        /// <param name="clean">The source field cleaner applied per line.</param>
        /// <returns>The text; empty when nothing readable remains.</returns>
        internal static string ToPlainText(INode root, Func<string?, string?> clean)
        {
            var raw = WalkToRawText(root);
            return JoinCleanedLines(raw, clean);
        }

        private static string WalkToRawText(INode root)
        {
            var builder = new StringBuilder();
            var pending = new Stack<(INode Node, bool Closing)>();
            pending.Push((root, false));
            while (pending.Count > 0)
            {
                var (node, closing) = pending.Pop();
                if (closing)
                {
                    builder.Append('\n');
                    continue;
                }
                if (node is IText text)
                {
                    builder.Append(text.Data);
                    continue;
                }
                if (node is IElement element && !ReferenceEquals(node, root))
                {
                    var name = element.LocalName;
                    if (NonTextElements.Contains(name))
                    {
                        continue;
                    }
                    if (name == "br")
                    {
                        builder.Append('\n');
                        continue;
                    }
                    if (BlockElements.Contains(name))
                    {
                        builder.Append('\n');
                        pending.Push((node, true));
                    }
                    if (name == "li")
                    {
                        builder.Append("- ");
                    }
                }
                var children = node.ChildNodes;
                for (var index = children.Length - 1; index >= 0; index--)
                {
                    pending.Push((children[index], false));
                }
            }
            return builder.ToString();
        }

        private static string JoinCleanedLines(string raw, Func<string?, string?> clean)
        {
            var lines = raw.Split('\n').Select(line => clean(line)).Where(IsContentLine);
            return string.Join('\n', lines);
        }

        private static bool IsContentLine(string? line) => line is not null && line != "-";
    }
}
