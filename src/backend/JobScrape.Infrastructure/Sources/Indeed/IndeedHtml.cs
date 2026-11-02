using System.Globalization;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// AngleSharp helpers shared by the Indeed parsers: lenient parsing, the
    /// HTML-judgeable visibility guards of docs/08-site-playbooks.md §1.3,
    /// and readable plain-text extraction.
    /// </summary>
    internal static class IndeedHtml
    {
        /// <summary>
        /// Opacity below which an element counts as invisible (playbook §1.3).
        /// </summary>
        private const double MinVisibleOpacity = 0.15;

        /// <summary>
        /// Elements whose content is never readable text.
        /// </summary>
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

        /// <summary>
        /// Elements that start a new line in readable text.
        /// </summary>
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
        /// Parses any string as an HTML document. The HTML5 parser recovers
        /// from malformed markup instead of throwing.
        /// </summary>
        /// <param name="html">The markup.</param>
        /// <returns>The document; the caller disposes it.</returns>
        public static IHtmlDocument Parse(string html)
        {
            return new HtmlParser().ParseDocument(html);
        }

        /// <summary>
        /// Whether an element, or any ancestor, is hidden in a way the markup
        /// alone shows: the browser pass's <c>data-jobscrape-hidden="true"</c>
        /// marker, the <c>hidden</c> attribute, <c>aria-hidden="true"</c>, or an
        /// inline <c>display:none</c>, <c>visibility:hidden|collapse</c>, or
        /// <c>opacity</c> below 0.15.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <returns>True when the element must be treated as invisible.</returns>
        public static bool IsHidden(IElement element)
        {
            for (var current = element; current is not null; current = current.ParentElement)
            {
                if (
                    IsTrue(current.GetAttribute("data-jobscrape-hidden"))
                    || current.HasAttribute("hidden")
                    || IsTrue(current.GetAttribute("aria-hidden"))
                    || HasHidingStyle(current.GetAttribute("style"))
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The first element under <paramref name="scope"/> matching any of
        /// the selectors, tried in order.
        /// </summary>
        /// <param name="scope">The element or document to search.</param>
        /// <param name="selectors">CSS selectors, most specific first.</param>
        /// <returns>The first match, or null.</returns>
        public static IElement? First(IParentNode scope, params string[] selectors)
        {
            foreach (var selector in selectors)
            {
                if (scope.QuerySelector(selector) is { } match)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// The element's whitespace-collapsed text, or null when blank.
        /// </summary>
        /// <param name="element">The element; null yields null.</param>
        /// <returns>The cleaned text, or null.</returns>
        public static string? TextOf(IElement? element)
        {
            return element is null ? null : IndeedFields.Clean(element.TextContent);
        }

        /// <summary>
        /// Converts an element's content to readable plain text: block
        /// elements and <c>&lt;br&gt;</c> start new lines, list items get a
        /// <c>- </c> bullet, scripts and styles are dropped, entities are
        /// decoded by the parser, whitespace inside a line collapses, and
        /// blank lines are removed. Iterative, so hostile nesting depth cannot
        /// overflow the stack.
        /// </summary>
        /// <param name="root">The element whose content is converted.</param>
        /// <returns>The text; empty when nothing readable remains.</returns>
        public static string ToPlainText(INode root)
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

            var lines = builder
                .ToString()
                .Split('\n')
                .Select(line => IndeedFields.Clean(line))
                .Where(line => line is not null && line != "-");
            return string.Join('\n', lines);
        }

        /// <summary>
        /// Whether an attribute value is <c>true</c>, ignoring case and padding.
        /// </summary>
        /// <param name="value">The attribute value.</param>
        /// <returns>True for <c>true</c>.</returns>
        private static bool IsTrue(string? value)
        {
            return string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether an inline style hides the element.
        /// </summary>
        /// <param name="style">The <c>style</c> attribute value.</param>
        /// <returns>True for display:none, visibility:hidden|collapse, or near-zero opacity.</returns>
        private static bool HasHidingStyle(string? style)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                return false;
            }

            foreach (var declaration in style.Split(';'))
            {
                var colon = declaration.IndexOf(':', StringComparison.Ordinal);
                if (colon <= 0)
                {
                    continue;
                }

                var property = declaration[..colon].Trim();
                var value = declaration[(colon + 1)..]
                    .Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
                var hides = property.ToUpperInvariant() switch
                {
                    "DISPLAY" => value.Equals("none", StringComparison.OrdinalIgnoreCase),
                    "VISIBILITY" => value.Equals("hidden", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("collapse", StringComparison.OrdinalIgnoreCase),
                    "OPACITY" => TryOpacity(value, out var opacity) && opacity < MinVisibleOpacity,
                    _ => false,
                };
                if (hides)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses a CSS opacity, either a number or a percentage.
        /// </summary>
        /// <param name="value">The CSS value.</param>
        /// <param name="opacity">The opacity as a 0–1 fraction.</param>
        /// <returns>True when the value parsed.</returns>
        private static bool TryOpacity(string value, out double opacity)
        {
            var percent = value.EndsWith('%');
            var number = percent ? value[..^1].Trim() : value;
            if (
                double.TryParse(
                    number,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out opacity
                )
            )
            {
                opacity = percent ? opacity / 100 : opacity;
                return true;
            }

            return false;
        }
    }
}
