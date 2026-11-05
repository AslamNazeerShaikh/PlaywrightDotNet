using System.Globalization;
using JobScrape.Application.Abstractions;
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
        private readonly IBrowserSessionPool _pool;
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
            IBrowserSessionPool pool,
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
            var page = SearchPageDriver.FindPage(_pool, context.BrowserProfileId);
            if (page is null)
            {
                return SearchPageResult.Blocked(
                    SearchPageDriver.Transient($"Browser page closed or crashed at page {request.PageNumber}.")
                );
            }

            try
            {
                var start = request.Cursor is null
                ? (request.PageNumber - 1) * LinkedInUrlBuilder.PageSize
                : SearchPageDriver.CursorOffset(request.Cursor);
                return await SearchPageDriver.SearchOffsetPageAsync<LinkedInSearchPage>(
                    page,
                    LinkedInUrlBuilder.SearchUrl(context.Criteria, start).AbsoluteUri,
                    CardSelector,
                    MarkHiddenScript,
                    start,
                    static (url, status, title, body, hasCards) =>
                        LinkedInChallengeClassifier.Classify(url, status, title, body, hasCards),
                    static (html, url, now) => LinkedInSearchParser.Parse(html, url, now),
                    static parsed => parsed.Jobs,
                    static parsed => parsed.DroppedCards,
                    static parsed => parsed.FirstJobId is null && parsed.ResultCount is null
                        ? null
                        : new PageFingerprint(parsed.FirstJobId, parsed.ResultCount),
                    static (parsed, offset) => parsed.HasNext
                        ? (offset + LinkedInUrlBuilder.PageSize).ToString(CultureInfo.InvariantCulture)
                        : null,
                    dropped => LogCardsDropped(
                        _logger,
                        context.RunId.Value,
                        request.PageNumber,
                        dropped
                    ),
                    _clock.GetUtcNow(),
                    cancellationToken
                );
            }
            catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(SearchPageDriver.Transient("Search page did not load."));
            }
            catch (TimeoutException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogNavigationFailed(_logger, ex, context.RunId.Value, $"page {request.PageNumber}");
                return SearchPageResult.Blocked(SearchPageDriver.Transient("Search page timed out."));
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
