using System.Globalization;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// The LinkedIn adapter (docs/08-site-playbooks.md §4): guest search
    /// only, low volume, stops on the first wall. High-risk tier: runs need
    /// <c>AcknowledgeSourceRisk=true</c>, <c>AllowHighRiskSources=true</c>,
    /// and interactive confirmation, enforced by the run gate before this
    /// adapter is ever called. Detail enrichment is unsupported on the guest
    /// adapter: <see cref="GetDetailAsync"/> is blocked without fetching.
    /// </summary>
    public sealed partial class LinkedInJobSource : IJobSource
    {
        private const string CardSelector =
            "li.jobs-search-results__list-item, div.job-card-container";

        // Marks cards a person could not see (computed style, size, far
        // off-screen) so the HTML parser skips honeypots (§1.3).
        private const string MarkHiddenScript = """
            () => {
              for (const card of document.querySelectorAll('li.jobs-search-results__list-item, div.job-card-container')) {
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

        // All injected and never mutated.
        private readonly BrowserSessionPool _pool;
        private readonly TimeProvider _clock;
        private readonly ILogger<LinkedInJobSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkedInJobSource"/> class.
        /// </summary>
        /// <param name="pool">Provides the run's page (opened by the session manager).</param>
        /// <param name="clock">The time source for relative posted dates.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public LinkedInJobSource(
            BrowserSessionPool pool,
            TimeProvider clock,
            ILogger<LinkedInJobSource> logger
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
        public SourceKey Key => SourceKey.LinkedIn;

        /// <inheritdoc />
        public SourceCapabilities Capabilities { get; } = new(SupportsDetails: false);

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
                    LinkedInUrlBuilder.SearchUrl(context.Criteria, start).AbsoluteUri,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );
                status = response?.Status;

                cancellationToken.ThrowIfCancellationRequested();
                var hasCards = await WaitForCardsAsync(page);
                await ScrollAsync(page, cancellationToken);
                await page.EvaluateAsync(MarkHiddenScript);

                var challenge = LinkedInChallengeClassifier.Classify(
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

                var parsed = LinkedInSearchParser.Parse(
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
                    var following = start + LinkedInUrlBuilder.PageSize;
                    next = following.ToString(CultureInfo.InvariantCulture);
                }

                return new SearchPageResult(
                    parsed.Jobs,
                    next,
                    parsed.FirstJobId is null && parsed.ResultCount is null
                        ? null
                        : new PageFingerprint(parsed.FirstJobId, parsed.ResultCount),
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
        public Task<JobDetailResult> GetDetailAsync(
            SourceRunContext context,
            JobReference job,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(job);

            // Guest detail pages sit behind the sign-in wall; the adapter
            // fails closed instead of fetching them.
            var blocked = new JobDetailResult(
                null,
                new ChallengeAssessment(ChallengeKind.LoginRequired, 1, "details:unsupported")
            );
            return Task.FromResult(blocked);
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
            Message = "LinkedIn navigation for run {RunId} failed at {Target}"
        )]
        private static partial void LogNavigationFailed(
            ILogger logger,
            Exception exception,
            Guid runId,
            string target
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "LinkedIn dropped {Count} malformed cards for run {RunId} on page {Page}"
        )]
        private static partial void LogCardsDropped(
            ILogger logger,
            Guid runId,
            int page,
            int count
        );
    }
}
