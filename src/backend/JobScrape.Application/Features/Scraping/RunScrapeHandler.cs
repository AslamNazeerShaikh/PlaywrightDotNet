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
    // partial is required by the LoggerMessage source generator, which emits
    // the Log* method bodies in a sibling generated declaration.
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

        /// <summary>
        /// Runs every gate, then creates and starts the run. Nothing is
        /// written unless every gate passes.
        /// </summary>
        /// <param name="request">The run request.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The started run's scope, or the first refusing gate's error.</returns>
        private async Task<Result<RunScope>> OpenScopeAsync(
            RunScrapeRequest request,
            CancellationToken cancellationToken
        )
        {
            var invalid = Validate(request);
            if (invalid is not null)
            {
                return Result.Fail<RunScope>(invalid);
            }

            var profile = await _stores.Profiles.GetAsync(request.ProfileId, cancellationToken);
            if (profile is null)
            {
                return Result.Fail<RunScope>(Errors.ProfileNotFound(request.ProfileId.Value));
            }

            if (!profile.Enabled)
            {
                return Result.Fail<RunScope>(Errors.ProfileDisabled(profile.Id.Value));
            }

            var source = profile.Source;
            var settings = _settings.Find(source);
            var adapter = _sources.Find(source);
            if (settings is null || adapter is null)
            {
                return Result.Fail<RunScope>(Errors.SourceUnavailable(source));
            }

            if (!settings.Enabled)
            {
                return Result.Fail<RunScope>(Errors.SourceDisabled(source));
            }

            var compliance = CompliancePolicy.CanRun(source, request.Consent);
            if (!compliance.IsAllowed)
            {
                return Result.Fail<RunScope>(Errors.ComplianceBlocked(source, compliance.Reason!));
            }

            var now = _clock.GetUtcNow();
            var circuit = await _stores.Circuits.FindAsync(source, cancellationToken);
            if (circuit is null)
            {
                circuit = SourceCircuitBreaker.Create(source);
                await _stores.Circuits.AddAsync(circuit, cancellationToken);
            }

            if (!circuit.AllowsRun)
            {
                return Result.Fail<RunScope>(Errors.SourceCoolingDown(source, circuit.OpenUntil));
            }

            var browser = await _stores.BrowserProfiles.GetAsync(
                request.BrowserProfileId,
                cancellationToken
            );
            if (browser is null)
            {
                return Result.Fail<RunScope>(
                    Errors.BrowserProfileNotFound(request.BrowserProfileId.Value)
                );
            }

            if (browser.Source != source)
            {
                return Result.Fail<RunScope>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]>
                        {
                            ["browserProfileId"] =
                            [
                                $"Browser profile is bound to '{browser.Source}', not '{source}'.",
                            ],
                        }
                    )
                );
            }

            if (!browser.CanLaunch(now))
            {
                return Result.Fail<RunScope>(
                    Errors.SessionUnavailable(
                        browser.Id.Value,
                        browser.State == SessionState.ResetRequired,
                        browser.CooldownUntil
                    )
                );
            }

            var quota = await FindOrStartQuotaAsync(source, now, cancellationToken);
            if (settings.DailyRunCap is { } runCap)
            {
                var runQuota = quota.TryStartRun(runCap, now);
                if (!runQuota.IsAllowed)
                {
                    return Result.Fail<RunScope>(Errors.QuotaExceeded(source, runQuota));
                }
            }
            else
            {
                quota.RecordRunStart(now);
            }

            var run = ScrapeRun.Create(profile, now);
            await _stores.Runs.AddAsync(run, cancellationToken);
            run.Start(now);
            browser.MarkUsed(now);
            await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
            LogStarted(_logger, run.Id.Value, source.Value, profile.Id.Value);

            var context = new SourceRunContext(
                run.Id,
                source,
                browser.Id,
                profile.Criteria,
                profile.Limits.StartPage
            );
            return Result.Ok(
                new RunScope(
                    run,
                    adapter,
                    settings,
                    browser,
                    quota,
                    circuit,
                    context,
                    request.Enrich
                )
            );
        }

        /// <summary>
        /// Checks the request shape before any lookup.
        /// </summary>
        /// <param name="request">The run request.</param>
        /// <returns>A validation error, or null when valid.</returns>
        private static ErrorInfo? Validate(RunScrapeRequest request)
        {
            var errors = new Dictionary<string, string[]>();
            if (request.ProfileId.Value == Guid.Empty)
            {
                errors["profileId"] = ["A search profile id is required."];
            }

            if (request.BrowserProfileId.Value == Guid.Empty)
            {
                errors["browserProfileId"] = ["A browser profile id is required."];
            }

            if (request.Consent is null)
            {
                errors["consent"] = ["Compliance consent is required."];
            }

            return errors.Count == 0
                ? null
                : Errors.ValidationFailed("The request contains invalid fields.", errors);
        }

        /// <summary>
        /// Loads today's quota for a source, starting the day when absent.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="now">The current time.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>Today's quota.</returns>
        private async Task<SourceQuota> FindOrStartQuotaAsync(
            SourceKey source,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var quota = await _stores.Quotas.FindAsync(source, today, cancellationToken);
            if (quota is null)
            {
                quota = SourceQuota.StartDay(source, now);
                await _stores.Quotas.AddAsync(quota, cancellationToken);
            }

            return quota;
        }

        /// <summary>
        /// Performs one source request, retrying transient errors only, with
        /// backoff and any stated <c>Retry-After</c>.
        /// </summary>
        /// <typeparam name="T">The request's result type.</typeparam>
        /// <param name="scope">The running run.</param>
        /// <param name="step">What is fetched, for logs, e.g. <c>page 2</c>.</param>
        /// <param name="fetch">Performs the request.</param>
        /// <param name="classify">Extracts the result's classification.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The last result: clean, a wall, or a transient error once retries are spent.</returns>
        private async Task<T> FetchWithRetryAsync<T>(
            RunScope scope,
            string step,
            Func<CancellationToken, Task<T>> fetch,
            Func<T, ChallengeAssessment> classify,
            CancellationToken cancellationToken
        )
        {
            for (var attempt = 1; ; attempt++)
            {
                var result = await fetch(cancellationToken);
                var challenge = classify(result);
                if (!_retry.ShouldRetry(challenge.Kind, attempt))
                {
                    return result;
                }

                var backoff = _retry.Backoff(attempt, challenge.RetryAfter);
                LogRetrying(
                    _logger,
                    scope.Run.Id.Value,
                    step,
                    attempt,
                    (long)backoff.TotalMilliseconds
                );
                await _delay.WaitAsync(backoff, cancellationToken);
            }
        }

        /// <summary>
        /// Ends searching on a wall or changed page. Rate limits wait out the
        /// retry window; changed pages fail the run with evidence; every
        /// other wall pauses the run for a human with evidence.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="assessment">The page classification.</param>
        /// <param name="decision">The policy decision.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the outcome is saved.</returns>
        private async Task StopOnWallAsync(
            RunScope scope,
            ChallengeAssessment assessment,
            ChallengeDecision decision,
            CancellationToken cancellationToken
        )
        {
            var run = scope.Run;
            var now = _clock.GetUtcNow();
            LogWall(
                _logger,
                run.Id.Value,
                scope.Context.Source.Value,
                assessment.Kind,
                assessment.Evidence
            );

            if (decision.Action == ChallengeAction.Pause)
            {
                // Pause is only decided for rate limits, always with a cooldown.
                scope.Quota.RecordChallenge(now);
                scope.Circuit.RecordFailure(assessment.Kind, now);
                run.RateLimit(now + decision.Cooldown.GetValueOrDefault(), now);
                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            scope.ArtifactDirectory = await _artifacts.CaptureAsync(
                scope.Context,
                assessment,
                cancellationToken
            );

            if (decision.Action == ChallengeAction.StopAndInspect)
            {
                // A changed page is a parser problem, not a wall: no
                // cooldown, no circuit failure.
                // StopAndInspect decisions always carry a reason.
                run.Fail(decision.Reason!, now);
                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            scope.Quota.RecordChallenge(now);
            BlameSession(
                scope,
                assessment.Kind,
                now,
                decision.Cooldown ?? scope.Settings.ChallengeCooldown
            );
            scope.Circuit.RecordFailure(assessment.Kind, now);
            run.PauseForChallenge(assessment.Kind, now);
            await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Records a wall against the browser profile. A login wall means the
        /// session expired (Healthy to Expired, then a manual re-login); a
        /// cooldown would only block that re-login. Every other wall cools
        /// the profile down.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="kind">The wall.</param>
        /// <param name="now">When it was hit.</param>
        /// <param name="cooldown">The cooldown for non-login walls.</param>
        private static void BlameSession(
            RunScope scope,
            ChallengeKind kind,
            DateTimeOffset now,
            TimeSpan cooldown
        )
        {
            if (kind != ChallengeKind.LoginRequired)
            {
                scope.Browser.MarkChallenged(kind, now, cooldown);
            }
            else if (scope.Browser.State == SessionState.Healthy)
            {
                scope.Browser.MarkExpired();
            }
        }

        /// <summary>
        /// Whether a classification is a session wall a profile can be blamed for.
        /// </summary>
        /// <param name="kind">The classification.</param>
        /// <returns>True for walls.</returns>
        private static bool IsWall(ChallengeKind kind)
        {
            return kind
                is not (
                    ChallengeKind.None
                    or ChallengeKind.TransientError
                    or ChallengeKind.SourceChanged
                );
        }

        /// <summary>
        /// Fetches pages until a cap, the last page, a repeated page, an
        /// exhausted quota, or a wall.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>True when a wall, changed page, or pagination stall ended the run; false when searching simply finished.</returns>
        private async Task<bool> SearchAsync(RunScope scope, CancellationToken cancellationToken)
        {
            var run = scope.Run;
            var politeness = new PolitenessPolicy(run.Limits, _random);
            var seenPages = new HashSet<PageFingerprint>();
            var retriedPages = new HashSet<int>();
            string? cursor = null;

            // A saved position is normally absent: each run gets a fresh id.
            // When present (an interrupted run picked back up), pagination
            // continues after the last completed page without recounting.
            // The position is part of the run aggregate, read here without
            // mutating: a fresh run stays rowless until its first kept page.
            var saved = run.Progress;
            var seenJobs = new HashSet<string>(
                saved?.SeenExternalIds ?? [],
                StringComparer.Ordinal
            );
            var completed = saved?.PagesCompleted ?? 0;
            cursor = saved?.NextCursor;

            // A cold load of a -N slug URL redirects to page 1, so
            // pre-window pages are visited, not kept (Naukri: Next
            // clicks; Indeed/LinkedIn: direct offsets).
            var firstPage = run.Limits.StartPage;
            var lastPage = firstPage + run.Limits.MaxPages - 1;

            // A fresh run starts at page one and walks forward; a resumed
            // run restarts at the window page its kept count points at.
            var absolute = completed == 0 ? 1 : firstPage + completed;
            if (absolute < firstPage)
            {
                LogAdvancingToStartPage(_logger, run.Id.Value, firstPage);
            }
            for (; absolute <= lastPage; absolute++)
            {
                if (run.Stats.PostingsFound >= run.Limits.MaxJobs)
                {
                    LogPaginationStopped(_logger, run.Id.Value, absolute, "job cap reached", "before");
                    break;
                }

                var pageQuota = scope.Quota.TryConsumePage(
                    scope.Settings.DailyPageCap,
                    _clock.GetUtcNow()
                );
                if (!pageQuota.IsAllowed)
                {
                    LogPaginationStopped(
                        _logger,
                        run.Id.Value,
                        absolute,
                        "daily page quota exhausted",
                        "before"
                    );
                    break;
                }

                if (completed > 0 || absolute > 1)
                {
                    await _delay.WaitAsync(politeness.NextDelay(), cancellationToken);
                }

                var page = absolute;
                var request = new SearchPageRequest(page, cursor);
                SearchPageResult result;
                try
                {
                    result = await FetchWithRetryAsync(
                        scope,
                        $"page {page}",
                        token => scope.Adapter.SearchPageAsync(scope.Context, request, token),
                        fetched => fetched.Challenge,
                        cancellationToken
                    );
                }
                catch (StartPageOutOfRangeException ex)
                {
                    // Fail with the precise reason instead of the generic
                    // zero-page fail below. The run is non-terminal here
                    // (searching just began), so Fail cannot throw.
                    var outOfRange =
                        $"start page {ex.StartPage} is past the last page {ex.MaxPage}";
                    LogPaginationStopped(_logger, run.Id.Value, page, outOfRange, "before");
                    run.RecordFailure();
                    run.Fail(ex.Message, _clock.GetUtcNow());
                    await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                    return true;
                }
                var decision = ChallengePolicy.Decide(
                    result.Challenge,
                    scope.Browser.ConsecutiveChallenges,
                    scope.Settings.ChallengeCooldown
                );
                if (decision.Action == ChallengeAction.Retry)
                {
                    // Retries are exhausted: skip the page and stop paginating.
                    run.RecordFailure();
                    await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                    LogPaginationStopped(_logger, run.Id.Value, page, "transient errors", "at");
                    break;
                }

                if (decision.Action != ChallengeAction.Continue)
                {
                    await StopOnWallAsync(scope, result.Challenge, decision, cancellationToken);
                    return true;
                }

                // A repeated fingerprint only stalls when Next is live: the
                // click did not advance. A missing Next ends the run through
                // the null cursor path below, so it is not a failure.
                if (
                    result.Fingerprint is not null
                    && result.NextCursor is not null
                    && !seenPages.Add(result.Fingerprint)
                )
                {
                    if (retriedPages.Add(page))
                    {
                        // The site sometimes serves stale content under the
                        // page URL; re-request the same page once through the
                        // loop top. The for increment restores absolute.
                        LogPageRetried(_logger, run.Id.Value, page);
                        absolute--;
                        continue;
                    }

                    // A repeated pre-window page is a stale re-serve, not a
                    // stall: fall through to the pre-window advance below,
                    // which keeps nothing.
                    if (page >= firstPage)
                    {
                        var range = result.Fingerprint.ResultRange;
                        var stalled = range is null
                            ? $"page {page} repeated an earlier page without advancing"
                            : $"page {page} repeated an earlier page without advancing ({range})";
                        LogPaginationStopped(_logger, run.Id.Value, page, stalled, "at");
                        run.RecordFailure();
                        run.Fail(stalled, _clock.GetUtcNow());
                        await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                        return true;
                    }
                }

                if (page < firstPage)
                {
                    // Before the window: advance the cursor, keep nothing.
                    cursor = result.NextCursor;
                    if (cursor is null)
                    {
                        LogPaginationStopped(
                            _logger,
                            run.Id.Value,
                            page,
                            "no further pages before the window",
                            "before"
                        );
                        break;
                    }

                    continue;
                }

                var now = _clock.GetUtcNow();
                var stats = await UpsertAsync(
                    scope,
                    result.Jobs,
                    run.Limits.MaxJobs - run.Stats.PostingsFound,
                    seenJobs,
                    now,
                    cancellationToken
                );
                run.RecordPage(stats, now);
                var nextCursor = result.NextCursor;
                // First mutation of the position happens here, on the first
                // kept page: nothing is written for a run that keeps nothing.
                run.ResumeProgress(now)
                    .RecordPage(run.Stats.PagesFetched, nextCursor, seenJobs, now);
                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
                LogPageRecorded(
                    _logger,
                    run.Id.Value,
                    page,
                    stats.PostingsFound,
                    stats.PostingsNew,
                    stats.PostingsUpdated
                );

                cursor = nextCursor;
                if (cursor is null)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates or refreshes the page's postings, up to the remaining job
        /// cap. Two profile gates run before anything is stored: cards older
        /// than the posted-within window are stale, and cards whose observed
        /// skills definitively miss the required list are out of scope.
        /// Postings with no observed skills stay candidates for enrichment.
        /// The gate reads the snapshot alone: a merge replaces stored skills
        /// with observed ones, so a passing snapshot leaves matching skills.
        /// A posting is counted once per run; a card that fails domain
        /// validation is dropped with a warning (docs/02-architecture.md §12.1).
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="jobs">The page's snapshots.</param>
        /// <param name="remaining">Postings still allowed by the job cap.</param>
        /// <param name="seenJobs">External ids already counted in this run.</param>
        /// <param name="now">The sighting time.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The page's posting counts.</returns>
        private async Task<PageStats> UpsertAsync(
            RunScope scope,
            IReadOnlyList<JobSnapshot> jobs,
            int remaining,
            HashSet<string> seenJobs,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            var source = scope.Context.Source;
            var criteria = scope.Context.Criteria;
            int found = 0,
                added = 0,
                updated = 0;

            foreach (var snapshot in jobs)
            {
                if (found >= remaining)
                {
                    break;
                }

                if (snapshot.Source != source)
                {
                    LogCardDropped(
                        _logger,
                        scope.Run.Id.Value,
                        snapshot.ExternalJobId,
                        "foreign source"
                    );
                    continue;
                }

                if (IsStale(criteria, snapshot, now))
                {
                    LogCardDropped(
                        _logger,
                        scope.Run.Id.Value,
                        snapshot.ExternalJobId,
                        "stale posted date"
                    );
                    continue;
                }

                try
                {
                    var id = ExternalJobId.Create(snapshot.ExternalJobId);
                    if (!seenJobs.Add(id.Value))
                    {
                        continue;
                    }

                    var existing = await _stores.Postings.FindAsync(source, id, cancellationToken);
                    if (IsSkillMismatch(criteria, snapshot.Skills))
                    {
                        LogCardDropped(
                            _logger,
                            scope.Run.Id.Value,
                            snapshot.ExternalJobId,
                            "skill mismatch"
                        );
                        continue;
                    }

                    ContentHash? before = null;
                    var posting = existing;
                    if (posting is null)
                    {
                        posting = JobPosting.Create(snapshot, now);
                        await _stores.Postings.AddAsync(posting, cancellationToken);
                    }
                    else
                    {
                        before = posting.ContentHash;
                        posting.Refresh(snapshot, now);
                    }

                    if (before is null)
                    {
                        added++;
                    }
                    else
                    {
                        updated += posting.ContentHash == before ? 0 : 1;
                    }

                    scope.Touched.Add(posting);
                    found++;
                }
                catch (DomainException ex)
                {
                    // Scraped input, not a programmer error: drop the card,
                    // keep the run, and leave a named reason in the log.
                    LogCardDropped(_logger, scope.Run.Id.Value, snapshot.ExternalJobId, ex.Message);
                }
            }

            return new PageStats(found, added, updated);
        }

        /// <summary>
        /// Tests a card against the profile's recency window at day
        /// granularity: card parsers stamp midnight UTC, so the window
        /// compares UTC dates, not instants.
        /// </summary>
        /// <param name="criteria">The profile's search criteria.</param>
        /// <param name="snapshot">The card snapshot.</param>
        /// <param name="now">The sighting time.</param>
        /// <returns>True when the card's UTC date is before today minus the window.</returns>
        private static bool IsStale(
            SearchCriteria criteria,
            JobSnapshot snapshot,
            DateTimeOffset now
        )
        {
            if (criteria.PostedWithinDays is not { } days)
            {
                return false;
            }

            // An unknown posted date passes: card parsers often lack one
            // and detail enrichment fills it in.
            if (snapshot.PostedAt is not { } postedAt)
            {
                return false;
            }

            // Card parsers stamp midnight UTC, so an instant comparison would
            // stale every card from the previous UTC day on a 1 day window.
            return postedAt.UtcDateTime.Date < now.UtcDateTime.Date.AddDays(-days);
        }

        /// <summary>
        /// Tests observed skill names against the profile's required list.
        /// Postings with no observed skills pass as enrichment candidates;
        /// only a definitive non-overlap drops the card.
        /// </summary>
        /// <param name="criteria">The profile's search criteria.</param>
        /// <param name="skillNames">The observed skill names.</param>
        /// <returns>True when skills were observed and none is required.</returns>
        private static bool IsSkillMismatch(
            SearchCriteria criteria,
            IEnumerable<string?> skillNames
        )
        {
            return criteria.RequiredSkills.Count > 0
                && skillNames.Any(name => !string.IsNullOrWhiteSpace(name))
                && !SkillMatcher.Matches(skillNames, criteria.RequiredSkills);
        }

        /// <summary>
        /// Postings worth a detail visit: touched in this run, never enriched
        /// (no description yet), in discovery order, up to the run's detail
        /// cap. Empty when enrichment is off, unsupported, or disabled by a
        /// zero cap, or when no page was fetched.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <returns>The postings to enrich.</returns>
        private static List<JobPosting> PendingEnrichment(RunScope scope)
        {
            var run = scope.Run;
            if (
                !scope.Enrich
                || !scope.Adapter.Capabilities.SupportsDetails
                || run.Limits.MaxDetails == 0
                || run.Stats.PagesFetched == 0
            )
            {
                return [];
            }

            return scope
                .Touched.Where(posting => posting.Description is null)
                .Take(run.Limits.MaxDetails)
                .ToList();
        }

        /// <summary>
        /// Visits detail pages and merges what they add. A transient or
        /// unusable detail is skipped; a rate limit or exhausted detail quota
        /// stops enrichment and lets the run complete with what it has (the
        /// run state machine only rate-limits while searching); any other
        /// wall pauses the run, and a changed page fails it.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="pending">The postings to enrich.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>True when a wall or changed page ended the run.</returns>
        private async Task<bool> EnrichAsync(
            RunScope scope,
            List<JobPosting> pending,
            CancellationToken cancellationToken
        )
        {
            var run = scope.Run;
            var politeness = new PolitenessPolicy(run.Limits, _random);

            for (var index = 0; index < pending.Count; index++)
            {
                var posting = pending[index];
                var detailQuota = scope.Quota.TryConsumeDetail(
                    scope.Settings.DailyDetailCap,
                    _clock.GetUtcNow()
                );
                if (!detailQuota.IsAllowed)
                {
                    LogEnrichmentStopped(
                        _logger,
                        run.Id.Value,
                        index + 1,
                        "daily detail quota exhausted"
                    );
                    break;
                }

                await _delay.WaitAsync(politeness.NextDelay(), cancellationToken);
                var job = new JobReference(posting.ExternalJobId, posting.Url);
                var result = await FetchWithRetryAsync(
                    scope,
                    $"detail {index + 1}",
                    token => scope.Adapter.GetDetailAsync(scope.Context, job, token),
                    detail => detail.Challenge,
                    cancellationToken
                );

                var decision = ChallengePolicy.Decide(
                    result.Challenge,
                    scope.Browser.ConsecutiveChallenges,
                    scope.Settings.ChallengeCooldown
                );
                if (decision.Action == ChallengeAction.Pause)
                {
                    var now = _clock.GetUtcNow();
                    scope.Quota.RecordChallenge(now);
                    scope.Circuit.RecordFailure(result.Challenge.Kind, now);
                    scope.HitWall = true;
                    LogEnrichmentStopped(_logger, run.Id.Value, index + 1, "rate limited");
                    break;
                }

                if (decision.Action is not (ChallengeAction.Continue or ChallengeAction.Retry))
                {
                    await StopOnWallAsync(scope, result.Challenge, decision, cancellationToken);
                    return true;
                }

                run.RecordDetail();
                if (!TryMerge(scope, posting, result, decision))
                {
                    run.RecordFailure();
                }

                await _stores.UnitOfWork.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// Merges a usable detail into its posting. Before merging, the
        /// card's posted date is cross-checked against the detail's: on a
        /// disagreement both values are logged and counted, and the detail
        /// date wins the merge.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="posting">The posting being enriched.</param>
        /// <param name="result">The fetched detail.</param>
        /// <param name="decision">The detail's policy decision.</param>
        /// <returns>False when the detail was transient, empty, or invalid.</returns>
        private bool TryMerge(
            RunScope scope,
            JobPosting posting,
            JobDetailResult result,
            ChallengeDecision decision
        )
        {
            if (decision.Action == ChallengeAction.Retry || result.Detail is null)
            {
                return false;
            }

            try
            {
                CrossCheckPostedDate(scope, posting, result.Detail);
                posting.Refresh(result.Detail, _clock.GetUtcNow());
                return true;
            }
            catch (DomainException ex)
            {
                // A detail for another posting or with invalid fields is
                // scraped input: skip it and keep the run.
                LogCardDropped(
                    _logger,
                    scope.Run.Id.Value,
                    posting.ExternalJobId.Value,
                    ex.Message
                );
                return false;
            }
        }

        /// <summary>
        /// Compares the card's posted date with the detail's before the
        /// detail merges. A disagreement is logged with both values and
        /// counted; the detail date wins because
        /// <see cref="JobPosting.Refresh"/> prefers observed fields.
        /// </summary>
        /// <param name="scope">The running run.</param>
        /// <param name="posting">The posting holding the card's date.</param>
        /// <param name="detail">The detail snapshot.</param>
        private void CrossCheckPostedDate(RunScope scope, JobPosting posting, JobSnapshot detail)
        {
            if (
                posting.PostedAt is not { } card
                || detail.PostedAt is not { } fresh
                || card == fresh
            )
            {
                return;
            }

            scope.Run.RecordPostedDateMismatch();
            LogPostedDateMismatch(
                _logger,
                scope.Run.Id.Value,
                posting.ExternalJobId.Value,
                card,
                fresh
            );
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
