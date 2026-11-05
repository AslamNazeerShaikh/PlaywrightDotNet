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
    }
}
