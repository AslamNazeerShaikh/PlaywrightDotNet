using System.Globalization;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Sources
{
    /// <summary>
    /// The verbatim-shared search-page driver for the source adapters:
    /// page lookup, card wait, reader scroll, transient evidence, cursor
    /// parsing, and the offset-search core (goto, classify, parse, dropped
    /// log). Per-source data (card selector, hidden-mark script, search URL,
    /// classifier, parser, next-cursor rule) stays in the adapters and
    /// arrives as arguments. Composition only; no base class, no hooks.
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

        /// <summary>
        /// Runs one offset-based search page: goto, card wait, scroll, hidden
        /// marking, challenge classification, parsing, and the dropped-cards
        /// log. Navigation failures propagate; the adapter logs and reports
        /// them with its own messages.
        /// </summary>
        /// <typeparam name="TPage">The source's parsed search-page type.</typeparam>
        /// <param name="page">The run's page.</param>
        /// <param name="searchUrl">The absolute search URL.</param>
        /// <param name="cardSelector">The source's result-card selector.</param>
        /// <param name="markHiddenScript">The source's hidden-marking script.</param>
        /// <param name="start">The zero-based result offset.</param>
        /// <param name="classify">The source's challenge classifier.</param>
        /// <param name="parse">The source's search parser.</param>
        /// <param name="jobs">Reads the parsed jobs.</param>
        /// <param name="droppedCards">Reads the parsed dropped-card count.</param>
        /// <param name="fingerprint">Builds the page fingerprint, or null.</param>
        /// <param name="nextCursor">The source's next-cursor rule.</param>
        /// <param name="logDropped">Logs a positive dropped-card count.</param>
        /// <param name="now">The time source reading for relative dates.</param>
        /// <param name="cancellationToken">Token to observe.</param>
        /// <returns>The fetched page.</returns>
        internal static async Task<SearchPageResult> SearchOffsetPageAsync<TPage>(
            IPage page,
            string searchUrl,
            string cardSelector,
            string markHiddenScript,
            int start,
            Func<Uri, int?, string, string, bool, ChallengeAssessment> classify,
            Func<string, string, DateTimeOffset, TPage> parse,
            Func<TPage, IReadOnlyList<JobSnapshot>> jobs,
            Func<TPage, int> droppedCards,
            Func<TPage, PageFingerprint?> fingerprint,
            Func<TPage, int, string?> nextCursor,
            Action<int> logDropped,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            var response = await page.GotoAsync(
                searchUrl,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
            );
            int? status = response?.Status;

            cancellationToken.ThrowIfCancellationRequested();
            var hasCards = await WaitForCardsAsync(page, cardSelector);
            await ScrollAsync(page, cancellationToken);
            await page.EvaluateAsync(markHiddenScript);

            var challenge = classify(
                new Uri(page.Url),
                status,
                await page.TitleAsync(),
                await PageText.BodyAsync(page),
                hasCards
            );
            if (challenge.Kind != ChallengeKind.None)
            {
                return SearchPageResult.Blocked(challenge);
            }

            var parsed = parse(await page.ContentAsync(), page.Url, now);
            var dropped = droppedCards(parsed);
            if (dropped > 0)
            {
                logDropped(dropped);
            }

            return new SearchPageResult(
                jobs(parsed),
                nextCursor(parsed, start),
                fingerprint(parsed),
                ChallengeAssessment.Clear
            );
        }
    }
}
