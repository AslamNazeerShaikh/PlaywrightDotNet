using System.Globalization;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// The Indeed adapter (docs/08-site-playbooks.md §5): drives the run's
    /// browser page and hands the HTML to the offline parsers. Page 1
    /// navigates to the search URL; later pages navigate directly with a
    /// larger <c>start</c> offset (the cursor is that offset). Walls are
    /// classified, never retried or solved here. Restricted tier: runs need
    /// <c>AcknowledgeSourceRisk=true</c>, enforced by the run gate before
    /// this adapter is ever called.
    /// </summary>
    public sealed partial class IndeedJobSource : IJobSource
    {
        private const string CardSelector =
            "div.job_seen_beacon, div[data-testid='slider_item']";

        // Marks cards a person could not see (computed style, size, far
        // off-screen) so the HTML parser skips honeypots (§1.3).
        private const string MarkHiddenScript = """
            () => {
              for (const card of document.querySelectorAll("div.job_seen_beacon, div[data-testid='slider_item']")) {
                const style = getComputedStyle(card);
                const box = card.getBoundingClientRect();
                const hidden = style.display === 'none'
                  || style.visibility === 'hidden' || style.visibility === 'collapse'
                  || Number(style.opacity) < 0.15
                  || box.width <= 1 || box.height <= 1
                  || box.left < -2000 || box.top + window.scrollY < -2000;
                if (hidden) card.setAttribute('data-jobscrape-hidden', 'true');
              }
            }
            """;

        private const string FingerprintScript = """
            () => [
              document.querySelector('div.job_seen_beacon, div[data-testid="slider_item"]')?.getAttribute('data-jk') ?? '',
              document.querySelector('.jobsearch-JobCountAndSortPane-jobCount, [data-testid="jobsearch-JobCountAndSortPane-jobCount"]')?.textContent?.trim() ?? ''
            ].join('|')
            """;

        // All injected and never mutated.
        private readonly BrowserSessionPool _pool;
        private readonly TimeProvider _clock;
        private readonly ILogger<IndeedJobSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndeedJobSource"/> class.
        /// </summary>
        /// <param name="pool">Provides the run's page (opened by the session manager).</param>
        /// <param name="clock">The time source for relative posted dates.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public IndeedJobSource(
            BrowserSessionPool pool,
            TimeProvider clock,
            ILogger<IndeedJobSource> logger
        )
        {
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _pool = pool;
            _clock = clock;
            _logger = logger;
        }

        /// <inheritdoc />
        public SourceKey Key => SourceKey.Indeed;

        /// <inheritdoc />
        public SourceCapabilities Capabilities { get; } = new(SupportsDetails: true);

        /// <inheritdoc />
        public async Task<SearchPageResult> SearchPageAsync(
            SourceRunContext context,
            SearchPageRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);
            var page = PageOf(context);

            int? status = null;
            try
            {
                var start = CursorOffset(request.Cursor);
                var response = await page.GotoAsync(
                    IndeedUrlBuilder.SearchUrl(context.Criteria, start).AbsoluteUri,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );
                status = response?.Status;

                cancellationToken.ThrowIfCancellationRequested();
                var hasCards = await WaitForCardsAsync(page);
                await ScrollAsync(page, cancellationToken);
                await page.EvaluateAsync(MarkHiddenScript);

                var challenge = IndeedChallengeClassifier.Classify(
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

                var parsed = IndeedSearchParser.Parse(
                    await page.ContentAsync(),
                    page.Url,
                    _clock.GetUtcNow()
                );
                if (parsed.DroppedCards > 0)
                {
                    LogCardsDropped(
                        _logger,
                        context.RunId.Value,
                        request.PageNumber,
                        parsed.DroppedCards
                    );
                }

                string? next = null;
                if (parsed.HasNext)
                {
                    var following = start + IndeedUrlBuilder.PageSize;
                    next = following.ToString(CultureInfo.InvariantCulture);
                }

                return new SearchPageResult(
                    parsed.Jobs,
                    next,
                    parsed.FirstJobKey is null && parsed.ResultCount is null
                        ? null
                        : new PageFingerprint(parsed.FirstJobKey, parsed.ResultCount),
                    ChallengeAssessment.Clear
                );
            }
            catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(Transient("Search page did not load."));
            }
            catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(Transient("Search page timed out."));
            }
        }

        /// <inheritdoc />
        public async Task<JobDetailResult> GetDetailAsync(
            SourceRunContext context,
            JobReference job,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(job);
            var page = PageOf(context);

            try
            {
                var response = await page.GotoAsync(
                    job.Url.Value,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );
                cancellationToken.ThrowIfCancellationRequested();
                await WaitForDetailAsync(page);

                var challenge = IndeedChallengeClassifier.Classify(
                    new Uri(page.Url),
                    response?.Status,
                    await page.TitleAsync(),
                    await PageText.BodyAsync(page),
                    // A detail page has no result cards; its own content
                    // is checked by the parser instead.
                    hasJobCards: true
                );
                if (challenge.Kind != ChallengeKind.None)
                {
                    return new JobDetailResult(null, challenge);
                }

                var detail = IndeedDetailParser.Parse(
                    await page.ContentAsync(),
                    job.Url.Value,
                    _clock.GetUtcNow()
                );
                return new JobDetailResult(detail, ChallengeAssessment.Clear);
            }
            catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, job.ExternalJobId.Value);
                return new JobDetailResult(null, Transient("Detail page did not load."));
            }
            catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, job.ExternalJobId.Value);
                return new JobDetailResult(null, Transient("Detail page timed out."));
            }
        }

        /// <summary>
        /// Reads the <c>start</c> offset from the previous page's cursor.
        /// </summary>
        /// <param name="cursor">The opaque cursor; null for the first page.</param>
        /// <returns>The result offset.</returns>
        private static int CursorOffset(string? cursor)
        {
            return cursor is null
                ? 0
                : int.TryParse(
                    cursor,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var start
                ) && start >= 0
                    ? start
                    : 0;
        }

        /// <summary>
        /// Gets the run's page, opened by the session manager before paging.
        /// </summary>
        /// <param name="context">The run context.</param>
        /// <returns>The page.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the profile has no open browser.</exception>
        private IPage PageOf(SourceRunContext context)
        {
            return _pool.Find(context.BrowserProfileId)
                ?? throw new InvalidOperationException(
                    $"No open browser for profile '{context.BrowserProfileId}'."
                );
        }

        /// <summary>
        /// Waits for result cards; a wall or an empty result never shows them.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>True when cards appeared.</returns>
        private static async Task<bool> WaitForCardsAsync(IPage page)
        {
            try
            {
                await page.WaitForSelectorAsync(
                    CardSelector,
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
        /// Waits for the detail's JSON-LD or description; the parser decides
        /// what is usable.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>A task that completes when content is present or after the wait.</returns>
        private static async Task WaitForDetailAsync(IPage page)
        {
            try
            {
                await page.WaitForSelectorAsync(
                    "script[type=\"application/ld+json\"], #jobDescriptionText",
                    new PageWaitForSelectorOptions
                    {
                        Timeout = 15_000,
                        State = WaitForSelectorState.Attached,
                    }
                );
            }
            catch (TimeoutException)
            {
                // Classified and parsed as-is below.
            }
        }

        /// <summary>
        /// Scrolls through the result list like a reader, so lazy cards load.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="cancellationToken">Token to observe between scrolls.</param>
        /// <returns>A task that completes after the scroll.</returns>
        private static async Task ScrollAsync(IPage page, CancellationToken cancellationToken)
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
        private static ChallengeAssessment Transient(string evidence)
        {
            return new ChallengeAssessment(ChallengeKind.TransientError, 0.5, evidence);
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Indeed navigation for run {RunId} failed at {Target}"
        )]
        private static partial void LogNavigationFailed(
            ILogger logger,
            Exception exception,
            Guid runId,
            string target
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Indeed dropped {Count} malformed cards for run {RunId} on page {Page}"
        )]
        private static partial void LogCardsDropped(
            ILogger logger,
            Guid runId,
            int page,
            int count
        );
    }
}
