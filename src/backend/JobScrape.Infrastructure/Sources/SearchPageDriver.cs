using System.Globalization;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Browser;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Sources
{
    /// <summary>
    /// The verbatim-shared search-page driver for the source adapters:
    /// page lookup, card wait, reader scroll, transient evidence, and cursor
    /// parsing. Per-source data (card selector, hidden-mark script, search URL,
    /// classifier, parser, next-cursor rule) stays in the adapters.
    /// Composition only; no base class, no hooks.
    /// </summary>
    internal static class SearchPageDriver
    {
        /// <summary>
        /// Gets the run's open page, or null when the browser was closed or
        /// its renderer died. A disposed context throws on property access,
        /// so that reads as gone too. Callers report a transient wall instead
        /// of throwing, so the run stops gracefully instead of crashing.
        /// </summary>
        /// <param name="pool">Provides the run's page.</param>
        /// <param name="profileId">The browser profile.</param>
        /// <returns>The open page, or null when there is none.</returns>
        internal static IPage? FindPage(IBrowserSessionPool pool, BrowserProfileId profileId)
        {
            try
            {
                return pool.Find(profileId);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the <c>start</c> offset from the previous page's cursor.
        /// </summary>
        /// <param name="cursor">The opaque cursor; null for the first page.</param>
        /// <returns>The result offset.</returns>
        internal static int CursorOffset(string? cursor)
        {
            return cursor is null ? 0
                : int.TryParse(
                    cursor,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var start
                )
                && start >= 0
                    ? start
                : 0;
        }

        /// <summary>
        /// Waits for result cards; a wall or an empty result never shows them.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="cardSelector">The source's result-card selector.</param>
        /// <returns>True when cards appeared.</returns>
        internal static async Task<bool> WaitForCardsAsync(IPage page, string cardSelector)
        {
            try
            {
                await page.WaitForSelectorAsync(
                    cardSelector,
                    new PageWaitForSelectorOptions { Timeout = 15_000 }
                );
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Scrolls through the result list like a reader, so lazy cards load.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="cancellationToken">Token to observe between scrolls.</param>
        /// <returns>A task that completes after the scroll.</returns>
        internal static async Task ScrollAsync(IPage page, CancellationToken cancellationToken)
        {
            for (var step = 0; step < 4; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.Mouse.WheelAsync(0, 900);
                await page.WaitForTimeoutAsync(400);
            }
        }

        /// <summary>
        /// A navigation failure the handler may retry with backoff.
        /// </summary>
        /// <param name="evidence">What failed.</param>
        /// <returns>A transient-error assessment.</returns>
        internal static ChallengeAssessment Transient(string evidence)
        {
            return new ChallengeAssessment(ChallengeKind.TransientError, 0.5, evidence);
        }
    }
}
