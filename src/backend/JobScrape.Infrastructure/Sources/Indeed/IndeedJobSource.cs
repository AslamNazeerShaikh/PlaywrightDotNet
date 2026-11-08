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
    public sealed partial class IndeedJobSource : IJobSource, ISourceWarmUpVerifier
    {
        private const string CardSelector = "div.job_seen_beacon, div[data-testid='slider_item']";

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
        private readonly IBrowserSessionPool _pool;
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
            IBrowserSessionPool pool,
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

        /// <summary>
        /// No warm-up: the restricted-tier guest session is reported ready
        /// without a verification fetch.
        /// </summary>
        /// <param name="page">The page the session manager opened.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>Null: there is no warm-up to run for this source.</returns>
        public Task<ChallengeAssessment?> VerifyAsync(
            IPage page,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(page);
            _ = cancellationToken;
            return Task.FromResult<ChallengeAssessment?>(null);
        }

        /// <inheritdoc />
        public async Task<SearchPageResult> SearchPageAsync(
            SourceRunContext context,
            SearchPageRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);
            var page = SearchPageDriver.FindPage(_pool, context.BrowserProfileId);
            if (page is null)
            {
                return SearchPageResult.Blocked(
                    SearchPageDriver.Transient(
                        $"Browser page closed or crashed at page {request.PageNumber}."
                    )
                );
            }

            try
            {
                var start = request.Cursor is null
                    ? (request.PageNumber - 1) * IndeedUrlBuilder.PageSize
                    : SearchPageDriver.CursorOffset(request.Cursor);
                // Read now before the fetch: the parser dates relative
                // posted marks against the fetch's start, as before.
                var now = _clock.GetUtcNow();
                var response = await page.GotoAsync(
                    IndeedUrlBuilder.SearchUrl(context.Criteria, start).AbsoluteUri,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );
                int? status = response?.Status;

                cancellationToken.ThrowIfCancellationRequested();
                var hasCards = await SearchPageDriver.WaitForCardsAsync(page, CardSelector);
                await SearchPageDriver.ScrollAsync(page, cancellationToken);
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

                var parsed = IndeedSearchParser.Parse(await page.ContentAsync(), page.Url, now);
                if (parsed.DroppedCards > 0)
                {
                    LogCardsDropped(
                        _logger,
                        context.RunId.Value,
                        request.PageNumber,
                        parsed.DroppedCards
                    );
                }

                return new SearchPageResult(
                    parsed.Jobs,
                    parsed.HasNext
                        ? (start + IndeedUrlBuilder.PageSize).ToString(CultureInfo.InvariantCulture)
                        : null,
                    parsed.FirstJobKey is null && parsed.ResultCount is null
                        ? null
                        : new PageFingerprint(parsed.FirstJobKey, parsed.ResultCount),
                    ChallengeAssessment.Clear
                );
            }
            catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(
                    SearchPageDriver.Transient("Search page did not load.")
                );
            }
            catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(
                    SearchPageDriver.Transient("Search page timed out.")
                );
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
            var page = SearchPageDriver.FindPage(_pool, context.BrowserProfileId);
            if (page is null)
            {
                return new JobDetailResult(
                    null,
                    SearchPageDriver.Transient(
                        $"Browser page closed or crashed at detail {job.ExternalJobId.Value}."
                    )
                );
            }

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
                return new JobDetailResult(
                    null,
                    SearchPageDriver.Transient("Detail page did not load.")
                );
            }
            catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, job.ExternalJobId.Value);
                return new JobDetailResult(
                    null,
                    SearchPageDriver.Transient("Detail page timed out.")
                );
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
