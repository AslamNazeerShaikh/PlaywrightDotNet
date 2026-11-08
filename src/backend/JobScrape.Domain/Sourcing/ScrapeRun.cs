using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Aggregate root for one execution of a search profile: a guarded
    /// lifecycle (docs/03-domain-model.md §6.1), monotonic stats, and the
    /// outcome. Illegal transitions throw <see cref="DomainException"/>;
    /// the application layer decides which transition to request.
    /// <para>
    /// The run snapshots the profile's source, limits, and company list at
    /// creation, so it can never point at a profile of another source and
    /// later profile edits never change a run in flight.
    /// </para>
    /// </summary>
    public sealed class ScrapeRun : AggregateRoot
    {
        // The active stage to return to after a challenge pause; set only
        // while PausedForChallenge.
        private RunStage? _resumeStage;

        /// <summary>
        /// Initializes a new pending run.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="profileId">The executed profile.</param>
        /// <param name="source">The profile's source.</param>
        /// <param name="limits">The profile's caps at creation.</param>
        /// <param name="companies">The profile's company list at creation.</param>
        /// <param name="createdAt">Creation time.</param>
        private ScrapeRun(
            ScrapeRunId id,
            SearchProfileId profileId,
            SourceKey source,
            RunLimits limits,
            IReadOnlyList<string> companies,
            DateTimeOffset createdAt
        )
        {
            Id = id;
            ProfileId = profileId;
            Source = source;
            Limits = limits;
            Companies = companies;
            CreatedAt = createdAt;
            Stage = RunStage.Pending;
            Stats = RunStats.Empty;
        }

        /// <summary>
        /// Gets the run identifier.
        /// </summary>
        public ScrapeRunId Id { get; }

        /// <summary>
        /// Gets the executed profile.
        /// </summary>
        public SearchProfileId ProfileId { get; }

        /// <summary>
        /// Gets the searched source, copied from the profile.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the caps this run enforces, snapshotted from the profile.
        /// </summary>
        public RunLimits Limits { get; }

        /// <summary>
        /// Gets the company names used to split this run's export,
        /// snapshotted from the profile.
        /// </summary>
        public IReadOnlyList<string> Companies { get; }

        /// <summary>
        /// Gets the current lifecycle stage.
        /// </summary>
        public RunStage Stage { get; private set; }

        /// <summary>
        /// Gets the run counters.
        /// </summary>
        public RunStats Stats { get; private set; }

        /// <summary>
        /// Gets the resumable fetch position, if any page was recorded.
        /// Part of this aggregate: loaded and saved only through the run.
        /// </summary>
        public ScrapeRunProgress? Progress { get; private set; }

        /// <summary>
        /// Gets when the run was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets when the run left <see cref="RunStage.Pending"/>, if it has.
        /// </summary>
        public DateTimeOffset? StartedAt { get; private set; }

        /// <summary>
        /// Gets when the run reached a terminal stage, if it has.
        /// </summary>
        public DateTimeOffset? FinishedAt { get; private set; }

        /// <summary>
        /// Gets the wall that paused the run, while it is paused.
        /// </summary>
        public ChallengeKind? PauseReason { get; private set; }

        /// <summary>
        /// Gets the earliest resume time, while the run is rate limited.
        /// </summary>
        public DateTimeOffset? RetryAt { get; private set; }

        /// <summary>
        /// Gets the operator-safe failure reason, once the run has failed.
        /// </summary>
        public string? FailureReason { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the run has ended and accepts no
        /// further transitions.
        /// </summary>
        public bool IsTerminal =>
            Stage is RunStage.Completed or RunStage.Failed or RunStage.Cancelled;

        /// <summary>
        /// Creates a pending run for an enabled profile.
        /// </summary>
        /// <param name="profile">The profile to execute. Must be enabled.</param>
        /// <param name="now">The creation time.</param>
        /// <returns>The pending run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when the profile is disabled.</exception>
        public static ScrapeRun Create(SearchProfile profile, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (!profile.Enabled)
            {
                throw new DomainException(
                    $"Profile '{profile.Id}' is disabled; runs cannot start."
                );
            }

            return new ScrapeRun(
                ScrapeRunId.New(),
                profile.Id,
                profile.Source,
                profile.Limits,
                profile.Criteria.Companies,
                now
            );
        }

        /// <summary>
        /// Starts the run: <see cref="RunStage.Pending"/> to
        /// <see cref="RunStage.StartingSession"/>. Compliance, quota, and
        /// circuit checks happen in the application layer before this call.
        /// </summary>
        /// <param name="now">The start time; not before creation.</param>
        /// <exception cref="DomainException">Thrown when not pending or <paramref name="now"/> precedes creation.</exception>
        public void Start(DateTimeOffset now)
        {
            EnsureStage(RunStage.Pending);
            if (now < CreatedAt)
            {
                throw new DomainException("A run cannot start before it was created.");
            }

            Stage = RunStage.StartingSession;
            StartedAt = now;
            Raise(new ScrapeRunStarted(Id, ProfileId, Source, now));
        }

        /// <summary>
        /// Marks the session healthy: <see cref="RunStage.StartingSession"/>
        /// to <see cref="RunStage.Searching"/>.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not starting a session.</exception>
        public void BeginSearching()
        {
            EnsureStage(RunStage.StartingSession);
            Stage = RunStage.Searching;
        }

        /// <summary>
        /// Returns the resumable fetch position, starting it when no page
        /// was recorded yet. Progress belongs to this run: the application
        /// layer never loads or saves it through its own port.
        /// </summary>
        /// <param name="now">The time a fresh position starts at.</param>
        /// <returns>The run's position, never null.</returns>
        public ScrapeRunProgress ResumeProgress(DateTimeOffset now)
        {
            return Progress ??= ScrapeRunProgress.Start(Id, now);
        }

        /// <summary>
        /// Records one fetched result page while searching.
        /// </summary>
        /// <param name="page">The page outcome. Must not be null.</param>
        /// <param name="now">When the page was recorded.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when not searching or the page cap is reached.</exception>
        public void RecordPage(PageStats page, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(page);
            EnsureStage(RunStage.Searching);

            // The cap is a politeness invariant, not a hint: the handler must
            // stop asking for pages before it is reached.
            if (Stats.PagesFetched >= Limits.MaxPages)
            {
                throw new DomainException($"Page cap of {Limits.MaxPages} reached.");
            }

            Stats = Stats.AddPage(page);
            Raise(new SearchPageFetched(Id, Stats.PagesFetched, page.PostingsFound, now));
        }

        /// <summary>
        /// Records a page or detail that failed and was skipped.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not searching or enriching.</exception>
        public void RecordFailure()
        {
            EnsureFetching();
            Stats = Stats.AddFailure();
        }

        /// <summary>
        /// Records a card/detail posted-date disagreement found while
        /// enriching. Counting is monotonic like every other stat.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not searching or enriching.</exception>
        public void RecordPostedDateMismatch()
        {
            EnsureFetching();
            Stats = Stats.AddPostedDateMismatch();
        }

        /// <summary>
        /// Moves from <see cref="RunStage.Searching"/> to
        /// <see cref="RunStage.Enriching"/>.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not searching or enrichment is disabled by a zero detail cap.</exception>
        public void BeginEnriching()
        {
            EnsureStage(RunStage.Searching);
            if (Limits.MaxDetails == 0)
            {
                throw new DomainException("Enrichment is disabled for this run (detail cap is 0).");
            }

            Stage = RunStage.Enriching;
        }

        /// <summary>
        /// Records one visited detail page while enriching.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not enriching or the detail cap is reached.</exception>
        public void RecordDetail()
        {
            EnsureStage(RunStage.Enriching);
            if (Stats.DetailsFetched >= Limits.MaxDetails)
            {
                throw new DomainException($"Detail cap of {Limits.MaxDetails} reached.");
            }

            Stats = Stats.AddDetail();
        }

        /// <summary>
        /// Moves from searching or enriching to <see cref="RunStage.Persisting"/>.
        /// </summary>
        /// <exception cref="DomainException">Thrown when not searching or enriching.</exception>
        public void BeginPersisting()
        {
            EnsureFetching();
            Stage = RunStage.Persisting;
        }

        /// <summary>
        /// Pauses for a wall that needs a human. Never auto-solved.
        /// </summary>
        /// <param name="kind">
        /// The wall: <see cref="ChallengeKind.SoftChallenge"/>,
        /// <see cref="ChallengeKind.Captcha"/>, <see cref="ChallengeKind.LoginRequired"/>,
        /// or <see cref="ChallengeKind.AccessDenied"/>. Rate limits use
        /// <see cref="RateLimit"/>; transient errors and changed pages are not walls.
        /// </param>
        /// <param name="now">When the wall was hit.</param>
        /// <exception cref="DomainException">Thrown when not searching or enriching, or <paramref name="kind"/> is not a wall.</exception>
        public void PauseForChallenge(ChallengeKind kind, DateTimeOffset now)
        {
            EnsureFetching();
            if (
                kind
                is not (
                    ChallengeKind.SoftChallenge
                    or ChallengeKind.Captcha
                    or ChallengeKind.LoginRequired
                    or ChallengeKind.AccessDenied
                )
            )
            {
                throw new DomainException($"Cannot pause for challenge kind {kind}.");
            }

            _resumeStage = Stage;
            PauseReason = kind;
            Stage = RunStage.PausedForChallenge;
            Stats = Stats.AddChallenge();
            Raise(new RunPausedForChallenge(Id, kind, now));
        }

        /// <summary>
        /// Waits out a source throttle: <see cref="RunStage.Searching"/> to
        /// <see cref="RunStage.RateLimited"/>.
        /// </summary>
        /// <param name="retryAt">Earliest resume time; not before <paramref name="now"/>.</param>
        /// <param name="now">When the throttle was hit.</param>
        /// <exception cref="DomainException">Thrown when not searching or <paramref name="retryAt"/> precedes <paramref name="now"/>.</exception>
        public void RateLimit(DateTimeOffset retryAt, DateTimeOffset now)
        {
            EnsureStage(RunStage.Searching);
            if (retryAt < now)
            {
                throw new DomainException("retryAt cannot be in the past.");
            }

            RetryAt = retryAt;
            Stage = RunStage.RateLimited;
            Raise(new RunRateLimited(Id, retryAt, now));
        }

        /// <summary>
        /// Resumes a paused run: a challenge pause returns to the stage it
        /// paused from; a rate limit returns to searching once the retry
        /// window has elapsed.
        /// </summary>
        /// <param name="now">The resume time.</param>
        /// <exception cref="DomainException">Thrown when not paused, or rate limited before <see cref="RetryAt"/>.</exception>
        public void Resume(DateTimeOffset now)
        {
            switch (Stage)
            {
                case RunStage.PausedForChallenge:
                    // _resumeStage is always set on entry to this stage.
                    Stage = _resumeStage!.Value;
                    _resumeStage = null;
                    PauseReason = null;
                    break;
                case RunStage.RateLimited:
                    // RetryAt is always set on entry to this stage.
                    var retryAt = RetryAt.GetValueOrDefault();
                    if (now < retryAt)
                    {
                        throw new DomainException($"Rate limited until {retryAt:O}.");
                    }

                    Stage = RunStage.Searching;
                    RetryAt = null;
                    break;
                default:
                    throw new DomainException($"Only a paused run can resume (stage {Stage}).");
            }
        }

        /// <summary>
        /// Completes a persisted run that fetched at least one page.
        /// </summary>
        /// <param name="now">The completion time; not before the start.</param>
        /// <exception cref="DomainException">Thrown when not persisting, no page was fetched, or <paramref name="now"/> precedes the start.</exception>
        public void Complete(DateTimeOffset now)
        {
            EnsureStage(RunStage.Persisting);
            if (Stats.PagesFetched == 0)
            {
                throw new DomainException("A run cannot complete without fetching a page.");
            }

            Finish(RunStage.Completed, now);
            Raise(new RunCompleted(Id, Stats, now));
        }

        /// <summary>
        /// Fails a non-terminal run with an operator-safe reason.
        /// </summary>
        /// <param name="reason">Why the run failed. Trimmed; must be non-blank.</param>
        /// <param name="now">The failure time; not before the start.</param>
        /// <exception cref="DomainException">Thrown when terminal, the reason is blank, or <paramref name="now"/> precedes the start.</exception>
        public void Fail(string? reason, DateTimeOffset now)
        {
            EnsureNotTerminal();
            var trimmed = reason?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new DomainException("A failure reason is required.");
            }

            FailureReason = trimmed;
            Finish(RunStage.Failed, now);
            Raise(new RunFailed(Id, trimmed, Stats, now));
        }

        /// <summary>
        /// Cancels a non-terminal run; partial results are kept.
        /// </summary>
        /// <param name="now">The cancellation time; not before the start.</param>
        /// <exception cref="DomainException">Thrown when terminal or <paramref name="now"/> precedes the start.</exception>
        public void Cancel(DateTimeOffset now)
        {
            EnsureNotTerminal();
            Finish(RunStage.Cancelled, now);
            Raise(new RunCancelled(Id, Stats, now));
        }

        /// <summary>
        /// Enters a terminal stage and clears pause state.
        /// </summary>
        /// <param name="terminal">The terminal stage.</param>
        /// <param name="now">The finish time.</param>
        /// <exception cref="DomainException">Thrown when <paramref name="now"/> precedes the start (or creation, if never started).</exception>
        private void Finish(RunStage terminal, DateTimeOffset now)
        {
            if (now < (StartedAt ?? CreatedAt))
            {
                throw new DomainException("A run cannot finish before it started.");
            }

            Stage = terminal;
            FinishedAt = now;
            PauseReason = null;
            RetryAt = null;
            _resumeStage = null;
        }

        /// <summary>
        /// Throws unless the run is in <paramref name="expected"/>.
        /// </summary>
        /// <param name="expected">The required stage.</param>
        /// <exception cref="DomainException">Thrown when the stage differs.</exception>
        private void EnsureStage(RunStage expected)
        {
            if (Stage != expected)
            {
                throw new DomainException($"Expected stage {expected}, got {Stage}.");
            }
        }

        /// <summary>
        /// Throws unless the run is searching or enriching.
        /// </summary>
        /// <exception cref="DomainException">Thrown in any other stage.</exception>
        private void EnsureFetching()
        {
            if (Stage is not (RunStage.Searching or RunStage.Enriching))
            {
                throw new DomainException($"Expected Searching or Enriching, got {Stage}.");
            }
        }

        /// <summary>
        /// Throws when the run has already ended.
        /// </summary>
        /// <exception cref="DomainException">Thrown when terminal.</exception>
        private void EnsureNotTerminal()
        {
            if (IsTerminal)
            {
                throw new DomainException($"Run is terminal ({Stage}).");
            }
        }
    }
}
