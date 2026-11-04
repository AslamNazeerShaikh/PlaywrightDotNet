using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// The Naukri adapter (docs/08-site-playbooks.md §3): drives the run's
    /// browser page and hands the HTML to the offline parsers. A null cursor
    /// always loads page 1 first: its range gives the listing size, so a
    /// start page past the end fails fast instead of recording page 1 as
    /// the window. A cold <c>-N</c> load redirects to filtered page 1, so
    /// deep links are never used; pre-window pages advance through native
    /// <b>Next</b> clicks. Later pages inside a run click Next and wait for
    /// the fingerprint to change. Walls are classified, never retried or
    /// solved here.
    /// </summary>
    public sealed partial class NaukriJobSource : IJobSource
    {
        /// <summary>
        /// The opaque cursor meaning "click Next on the current page".
        /// </summary>
        public const string NextCursor = "next";

        private const string CardSelector = "div.srp-jobtuple-wrapper[data-job-id]";

        // Marks cards a person could not see (computed style, size, far
        // off-screen) so the HTML parser skips honeypots (§1.3).
        private const string MarkHiddenScript = """
            () => {
              for (const card of document.querySelectorAll('div.srp-jobtuple-wrapper[data-job-id]')) {
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
              document.querySelector('div.srp-jobtuple-wrapper[data-job-id]')?.getAttribute('data-job-id') ?? '',
              document.querySelector('[class*="count-string"]')?.textContent?.trim() ?? ''
            ].join('|')
            """;

        // Fingerprint polls after clicking Next: 40 x 500 ms = 20 s (§3.5).
        private const int PageChangePolls = 40;

        // All injected and never mutated.
        private readonly BrowserSessionPool _pool;
        private readonly TimeProvider _clock;
        private readonly ILogger<NaukriJobSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NaukriJobSource"/> class.
        /// </summary>
        /// <param name="pool">Provides the run's page (opened by the session manager).</param>
        /// <param name="clock">The time source for relative posted dates.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public NaukriJobSource(
            BrowserSessionPool pool,
            TimeProvider clock,
            ILogger<NaukriJobSource> logger
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
        public SourceKey Key => SourceKey.Naukri;

        /// <inheritdoc />
        public SourceCapabilities Capabilities { get; } = new(SupportsDetails: true);

        /// <inheritdoc />
        /// <exception cref="StartPageOutOfRangeException">
        /// Thrown when the requested start page is past the listing's last page.
        /// </exception>
        public async Task<SearchPageResult> SearchPageAsync(
            SourceRunContext context,
            SearchPageRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);
            var page = ClosedPage(context.BrowserProfileId);
            if (page is null)
            {
                // The operator closed the browser, or its renderer died,
                // mid-run. A transient stop beats a crash: the handler
                // retries once, then ends the run with whatever it kept.
                return SearchPageResult.Blocked(
                    Transient($"Browser page closed or crashed at page {request.PageNumber}.")
                );
            }

            try
            {
                if (request.Cursor is null)
                {
                    // Page 1 is always loaded first to learn the listing
                    // size: a cold -N load redirects to filtered page 1.
                    var page1Url = NaukriUrlBuilder.SearchUrl(context.Criteria);
                    LogSearchUrl(
                        _logger,
                        context.RunId.Value,
                        request.PageNumber,
                        page1Url.AbsoluteUri
                    );
                    var probe = await page.GotoAsync(
                        page1Url.AbsoluteUri,
                        new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                    );
                    cancellationToken.ThrowIfCancellationRequested();
                    var first = await FinishPageAsync(
                        page,
                        context,
                        request,
                        probe?.Status,
                        cancellationToken
                    );
                    var landedUrl = page.Url;
                    LogLanded(_logger, context, request, landedUrl, first);
                    if (
                        first.Challenge.Kind == ChallengeKind.None && context.StartPage > 1
                    )
                    {
                        var size = NaukriResultRange.Parse(first.Fingerprint?.ResultRange);
                        if (size is not null && context.StartPage > size.MaxPage)
                        {
                            throw new StartPageOutOfRangeException(
                                context.StartPage,
                                size.MaxPage
                            );
                        }

                        if (size is null && first.NextCursor is null)
                        {
                            // One page and no parseable range: the listing
                            // has a single page, so any later start is past it.
                            throw new StartPageOutOfRangeException(context.StartPage, 1);
                        }
                    }

                    return first;
                }

                if (!await ClickNextAsync(page, cancellationToken))
                {
                    // Next vanished or the page did not change: report the
                    // same page so the handler's repeated-page guard stops.
                    LogNextDidNotAdvance(_logger, context.RunId.Value, request.PageNumber);
                }

                return await FinishPageAsync(page, context, request, null, cancellationToken);
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

        /// <summary>
        /// Logs where a cold fetch landed with its range.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="context">The run context.</param>
        /// <param name="request">The requested page.</param>
        /// <param name="url">The landed URL.</param>
        /// <param name="result">The parsed page.</param>
        private static void LogLanded(
            ILogger<NaukriJobSource> logger,
            SourceRunContext context,
            SearchPageRequest request,
            string url,
            SearchPageResult result
        )
        {
            LogLandedPage(
                logger,
                context.RunId.Value,
                request.PageNumber,
                url,
                result.Fingerprint?.ResultRange ?? "unknown"
            );
        }

        /// <summary>
        /// Reads the current page: waits for cards, scrolls, marks hidden
        /// cards, classifies, and parses. The navigation status comes from
        /// the goto that led here, or null after Next clicks.
        /// </summary>
        /// <param name="page">The run's page.</param>
        /// <param name="context">The run context, for logs.</param>
        /// <param name="request">The requested page, for logs.</param>
        /// <param name="status">The navigation status, if any.</param>
        /// <param name="cancellationToken">Token to observe.</param>
        /// <returns>The fetched page.</returns>
        private async Task<SearchPageResult> FinishPageAsync(
            IPage page,
            SourceRunContext context,
            SearchPageRequest request,
            int? status,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasCards = await WaitForCardsAsync(page);
            await ScrollAsync(page, cancellationToken);
            await page.EvaluateAsync(MarkHiddenScript);

            var challenge = NaukriChallengeClassifier.Classify(
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

            var parsed = NaukriSearchParser.Parse(
                await page.ContentAsync(),
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

            return new SearchPageResult(
                parsed.Jobs,
                parsed.HasNext ? NextCursor : null,
                parsed.FirstJobId is null && parsed.ResultRange is null
                    ? null
                    : new PageFingerprint(parsed.FirstJobId, parsed.ResultRange),
                ChallengeAssessment.Clear
            );
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
            var page = ClosedPage(context.BrowserProfileId);
            if (page is null)
            {
                return new JobDetailResult(
                    null,
                    Transient($"Browser page closed or crashed at detail {job.ExternalJobId.Value}.")
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

                var challenge = NaukriChallengeClassifier.Classify(
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

                var detail = NaukriDetailParser.Parse(
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
        /// Gets the run's open page, or null when the browser was closed or
        /// its renderer died. A disposed context throws on property access,
        /// so that reads as gone too. Callers report a transient wall instead
        /// of throwing, so the run stops gracefully instead of crashing.
        /// </summary>
        /// <param name="profileId">The browser profile.</param>
        /// <returns>The open page, or null when there is none.</returns>
        private IPage? ClosedPage(BrowserProfileId profileId)
        {
            try
            {
                return _pool.Find(profileId);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Clicks the native Next and waits until the first card or the
        /// result range changes.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="cancellationToken">Token to observe between polls.</param>
        /// <returns>True when the page advanced.</returns>
        private static async Task<bool> ClickNextAsync(
            IPage page,
            CancellationToken cancellationToken
        )
        {
            var before = await page.EvaluateAsync<string>(FingerprintScript);
            var next = page.GetByRole(AriaRole.Link, new() { Name = "Next", Exact = true });
            if (
                await next.CountAsync() == 0
                || await next.First.GetAttributeAsync("aria-disabled") == "true"
            )
            {
                return false;
            }

            await next.First.ClickAsync();
            for (var poll = 0; poll < PageChangePolls; poll++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.WaitForTimeoutAsync(500);
                if (await page.EvaluateAsync<string>(FingerprintScript) != before)
                {
                    return true;
                }
            }

            return false;
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
                    "script[type=\"application/ld+json\"], [class*=\"JDC__dang-inner-html\"]",
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
            Message = "Naukri navigation for run {RunId} failed at {Target}"
        )]
        private static partial void LogNavigationFailed(
            ILogger logger,
            Exception exception,
            Guid runId,
            string target
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri Next did not advance for run {RunId} on page {Page}"
        )]
        private static partial void LogNextDidNotAdvance(ILogger logger, Guid runId, int page);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri run {RunId} page {Page} navigates to {Url}"
        )]
        private static partial void LogSearchUrl(
            ILogger logger,
            Guid runId,
            int page,
            string url
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri run {RunId} page {Page} landed on {Url} with range {Range}"
        )]
        private static partial void LogLandedPage(
            ILogger logger,
            Guid runId,
            int page,
            string url,
            string range
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri dropped {Count} malformed cards for run {RunId} on page {Page}"
        )]
        private static partial void LogCardsDropped(
            ILogger logger,
            Guid runId,
            int page,
            int count
        );
    }
}
