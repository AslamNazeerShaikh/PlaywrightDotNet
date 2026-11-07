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
    public sealed partial class RunScrapeHandler
    {
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
    }
}
