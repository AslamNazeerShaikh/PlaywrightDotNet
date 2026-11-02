using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Bounded reads of page text for wall classification.
    /// </summary>
    internal static class PageText
    {
        /// <summary>
        /// Most body text classification reads: walls say so early.
        /// </summary>
        public const int MaxBodyChars = 20_000;

        /// <summary>
        /// Reads the visible body text, capped at <see cref="MaxBodyChars"/>.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The text; empty when the page has no body yet.</returns>
        public static async Task<string> BodyAsync(IPage page)
        {
            var text = await page.EvaluateAsync<string?>(
                "max => (document.body?.innerText ?? '').slice(0, max)",
                MaxBodyChars
            );
            return text ?? string.Empty;
        }
    }
}
