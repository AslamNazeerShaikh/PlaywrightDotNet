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
    }
}
