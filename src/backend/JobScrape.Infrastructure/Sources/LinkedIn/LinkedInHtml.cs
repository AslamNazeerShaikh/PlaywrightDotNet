using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// AngleSharp helpers shared by the LinkedIn parsers: lenient parsing
    /// and the HTML-judgeable visibility guards of docs/08-site-playbooks.md §1.3.
    /// </summary>
    internal static class LinkedInHtml
    {
        /// <summary>
        /// Opacity below which an element counts as invisible (playbook §1.3).
        /// </summary>
        private const double MinVisibleOpacity = 0.15;

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
            return element is null ? null : LinkedInFields.Clean(element.TextContent);
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
