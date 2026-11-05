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
    }
}
