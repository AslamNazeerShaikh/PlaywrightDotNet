using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Logging;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Executes one search profile end to end (docs/02-architecture.md §7.1,
    /// §8.1.1): gates, session check, pagination, upsert, and outcome. Called
    /// directly by the CLI or API; no mediator.
    /// <para>
    /// Gates that refuse before a run exists return a typed failure. Once a
    /// run exists the result is a success carrying the run's final stage:
    /// a paused, rate-limited, or failed run is a recorded outcome, not an
    /// exception. Walls are never retried; only transient errors are, within
    /// <see cref="RetryPolicy"/>. Changes are saved after every page, and
    /// cancellation records the run as cancelled before propagating.
    /// </para>
    /// </summary>
    public sealed partial class RunScrapeHandler
    {
        // All injected and never mutated: the handler is stateless; per-run
        // state lives in RunScope.
        private readonly ScrapeStores _stores;
        private readonly JobSourceRegistry _sources;
        private readonly SourceSettingsCatalog _settings;
        private readonly ISessionManager _sessions;
        private readonly IArtifactStore _artifacts;
        private readonly IDelay _delay;
        private readonly RetryPolicy _retry;
        private readonly IRandomSource _random;
        private readonly TimeProvider _clock;
        private readonly ILogger<RunScrapeHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunScrapeHandler"/> class.
        /// </summary>
        /// <param name="stores">The persistence ports.</param>
        /// <param name="sources">The source adapters.</param>
        /// <param name="settings">The per-source operating limits.</param>
        /// <param name="sessions">The browser session port.</param>
        /// <param name="artifacts">The evidence store.</param>
        /// <param name="delay">The waiting port.</param>
        /// <param name="retry">The transient-retry policy.</param>
        /// <param name="random">The jitter source for politeness delays.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunScrapeHandler(
            ScrapeStores stores,
            JobSourceRegistry sources,
            SourceSettingsCatalog settings,
            ISessionManager sessions,
            IArtifactStore artifacts,
            IDelay delay,
            RetryPolicy retry,
            IRandomSource random,
            TimeProvider clock,
            ILogger<RunScrapeHandler> logger
        )
        {
            ArgumentNullException.ThrowIfNull(stores);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(sessions);
            ArgumentNullException.ThrowIfNull(artifacts);
            ArgumentNullException.ThrowIfNull(delay);
            ArgumentNullException.ThrowIfNull(retry);
            ArgumentNullException.ThrowIfNull(random);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _stores = stores;
            _sources = sources;
            _settings = settings;
            _sessions = sessions;
            _artifacts = artifacts;
            _delay = delay;
            _retry = retry;
            _random = random;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Runs a search profile.
        /// </summary>
        /// <param name="request">The run request. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe; cancels the run cooperatively.</param>
        /// <returns>
        /// Success with the run summary once a run has started (in any final
        /// stage); otherwise a failure: <c>VALIDATION_FAILED</c>,
        /// <c>PROFILE_NOT_FOUND</c>, <c>PROFILE_DISABLED</c>,
        /// <c>SOURCE_UNAVAILABLE</c>, <c>SOURCE_DISABLED</c>,
        /// <c>COMPLIANCE_BLOCKED</c>, <c>SOURCE_COOLING_DOWN</c>,
        /// <c>BROWSER_PROFILE_NOT_FOUND</c>, <c>SESSION_COOLING_DOWN</c>,
        /// <c>SESSION_RESET_REQUIRED</c>, or <c>QUOTA_EXCEEDED</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown on cancellation, after the run is recorded as cancelled with
        /// its partial results.
        /// </exception>
        public Task<Result<ScrapeRunSummary>> HandleAsync(
            RunScrapeRequest request,
            CancellationToken cancellationToken
        )
        {
            return HandleAsync(request, null, cancellationToken);
        }

        /// <summary>
        /// Executes one scrape run like <see cref="HandleAsync(RunScrapeRequest, CancellationToken)"/>,
        /// reporting the run id as soon as every gate passed and the run is
        /// recorded. A background host returns that id to its caller while
        /// the scrape continues.
        /// </summary>
        /// <param name="request">The run request. Must not be null.</param>
        /// <param name="started">Called once with the new run's id after its first save; not called when a gate refuses the run.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The same outcomes as <see cref="HandleAsync(RunScrapeRequest, CancellationToken)"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown on cancellation, after the run is recorded as cancelled with
        /// its partial results.
        /// </exception>
        public async Task<Result<ScrapeRunSummary>> HandleAsync(
            RunScrapeRequest request,
            Action<ScrapeRunId>? started,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            var gate = await OpenScopeAsync(request, cancellationToken);
            if (gate.IsFailure)
            {
                LogRefused(_logger, request.ProfileId.Value, gate.Error.Code);
                return Result.Fail<ScrapeRunSummary>(gate.Error);
            }

            var scope = gate.Value;
            started?.Invoke(scope.Run.Id);
            try
            {
                await ExecuteAsync(scope, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!scope.Run.IsTerminal)
                {
                    scope.Run.Cancel(_clock.GetUtcNow());
                }

                // The caller's token is already cancelled, yet the cancelled
                // run and its partial results must still be recorded.
                await _stores.UnitOfWork.SaveChangesAsync(CancellationToken.None);
                LogCancelled(_logger, scope.Run.Id.Value, scope.Run.Stats.PagesFetched);
                throw;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Never leave a zombie run in an active stage: record the
                // failure with a safe reason (details stay in the log), then
                // let the exception reach the edge's global handler.
                LogUnexpected(_logger, ex, scope.Run.Id.Value);
                if (!scope.Run.IsTerminal)
                {
                    scope.Run.Fail("Unexpected error; see logs.", _clock.GetUtcNow());
                }

                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                throw;
            }

            LogFinished(
                _logger,
                scope.Run.Id.Value,
                scope.Run.Stage,
                scope.Run.Stats.PagesFetched,
                scope.Run.Stats.PostingsFound
            );
            return Result.Ok(ScrapeRunSummary.From(scope.Run, scope.ArtifactDirectory));
        }

        /// <summary>
        /// Verifies the session, paginates, and finishes the run.
        /// </summary>
        /// <param name="scope">The started run.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the run reaches its final stage.</returns>
        private async Task ExecuteAsync(RunScope scope, CancellationToken cancellationToken)
        {
            var run = scope.Run;
            var session = await _sessions.EnsureSessionAsync(
                scope.Context.Source,
                scope.Browser.Id,
                cancellationToken
            );
            if (session.Kind != ChallengeKind.None)
            {
                var now = _clock.GetUtcNow();
                if (IsWall(session.Kind))
                {
                    BlameSession(scope, session.Kind, now, scope.Settings.ChallengeCooldown);
                }

                // StartingSession -> Failed: a bootstrap failure is never retried.
                run.Fail($"Session not ready: {session.Kind}.", now);
                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            run.BeginSearching();
            if (await SearchAsync(scope, cancellationToken))
            {
                return;
            }

            var pending = PendingEnrichment(scope);
            if (pending.Count > 0)
            {
                run.BeginEnriching();
                if (await EnrichAsync(scope, pending, cancellationToken))
                {
                    return;
                }
            }

            var finishedAt = _clock.GetUtcNow();
            if (!scope.HitWall)
            {
                // A run that met a wall (e.g. a rate limit while enriching)
                // must not reset the breaker's failure streak.
                scope.Circuit.RecordSuccess(finishedAt);
            }

            run.BeginPersisting();
            if (run.Stats.PagesFetched == 0)
            {
                run.Fail("No result page could be fetched.", finishedAt);
            }
            else
            {
                run.Complete(finishedAt);
            }

            await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Scrape run {RunId} started for source {Source} and profile {ProfileId}"
        )]
        private static partial void LogStarted(
            ILogger logger,
            Guid runId,
            string source,
            Guid profileId
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Scrape request for profile {ProfileId} refused with {ErrorCode}"
        )]
        private static partial void LogRefused(ILogger logger, Guid profileId, string errorCode);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} advancing to start page {StartPage}: earlier pages are visited, not kept"
        )]
        private static partial void LogAdvancingToStartPage(
            ILogger logger,
            Guid runId,
            int startPage
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} page {Page}: {Found} postings, {New} new, {Updated} updated"
        )]
        private static partial void LogPageRecorded(
            ILogger logger,
            Guid runId,
            int page,
            int found,
            int @new,
            int updated
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} stopped paginating {Preposition} page {Page}: {Reason}"
        )]
        private static partial void LogPaginationStopped(
            ILogger logger,
            Guid runId,
            int page,
            string reason,
            string preposition
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Run {RunId} retrying page {Page} once: repeated page fingerprint with a live Next cursor"
        )]
        private static partial void LogPageRetried(ILogger logger, Guid runId, int page);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} stopped enriching before detail {Detail}: {Reason}"
        )]
        private static partial void LogEnrichmentStopped(
            ILogger logger,
            Guid runId,
            int detail,
            string reason
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Run {RunId} {Step} attempt {Attempt} failed transiently; retrying in {DelayMs} ms"
        )]
        private static partial void LogRetrying(
            ILogger logger,
            Guid runId,
            string step,
            int attempt,
            long delayMs
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Run {RunId} hit {ChallengeKind} on {Source}: {Evidence}"
        )]
        private static partial void LogWall(
            ILogger logger,
            Guid runId,
            string source,
            ChallengeKind challengeKind,
            string evidence
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Run {RunId} dropped card {ExternalJobId}: {Reason}"
        )]
        private static partial void LogCardDropped(
            ILogger logger,
            Guid runId,
            string externalJobId,
            string reason
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Run {RunId} card/detail posted-date mismatch for {ExternalJobId}: card {CardPostedAt} vs detail {DetailPostedAt}; detail wins"
        )]
        private static partial void LogPostedDateMismatch(
            ILogger logger,
            Guid runId,
            string externalJobId,
            DateTimeOffset cardPostedAt,
            DateTimeOffset detailPostedAt
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} finished in {Stage} after {Pages} pages and {Postings} postings"
        )]
        private static partial void LogFinished(
            ILogger logger,
            Guid runId,
            RunStage stage,
            int pages,
            int postings
        );

        [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed unexpectedly")]
        private static partial void LogUnexpected(ILogger logger, Exception exception, Guid runId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Run {RunId} cancelled after {Pages} pages; partial results kept"
        )]
        private static partial void LogCancelled(ILogger logger, Guid runId, int pages);

        /// <summary>
        /// Per-run state shared by the steps of one execution.
        /// </summary>
        private sealed class RunScope(
            ScrapeRun run,
            IJobSource adapter,
            SourceSettings settings,
            BrowserProfile browser,
            SourceQuota quota,
            SourceCircuitBreaker circuit,
            SourceRunContext context,
            bool enrich
        )
        {
            public ScrapeRun Run { get; } = run;

            public IJobSource Adapter { get; } = adapter;

            public SourceSettings Settings { get; } = settings;

            public BrowserProfile Browser { get; } = browser;

            public SourceQuota Quota { get; } = quota;

            public SourceCircuitBreaker Circuit { get; } = circuit;

            public SourceRunContext Context { get; } = context;

            public bool Enrich { get; } = enrich;

            public List<JobPosting> Touched { get; } = [];

            public bool HitWall { get; set; }

            public string? ArtifactDirectory { get; set; }
        }
    }
}
