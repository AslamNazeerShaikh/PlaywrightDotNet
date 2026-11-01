using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;

namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// Snapshots every configured source: policy, compliance gate, today's
    /// quota, and circuit state (docs/12-web-cockpit.md §12.2, §14.1).
    /// Read-only: a source that never ran shows zero usage and a closed
    /// circuit without anything being written.
    /// </summary>
    public sealed class ListSourcesHandler
    {
        // All injected and never mutated.
        private readonly SourceSettingsCatalog _sources;
        private readonly ComplianceConsent _consent;
        private readonly ISourceQuotaRepository _quotas;
        private readonly ISourceCircuitBreakerRepository _circuits;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSourcesHandler"/> class.
        /// </summary>
        /// <param name="sources">The configured sources.</param>
        /// <param name="consent">
        /// The configured consent flags. Interactive confirmation is per run,
        /// so it is false here and high-risk sources always report it as missing.
        /// </param>
        /// <param name="quotas">Daily quota storage.</param>
        /// <param name="circuits">Circuit breaker storage.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public ListSourcesHandler(
            SourceSettingsCatalog sources,
            ComplianceConsent consent,
            ISourceQuotaRepository quotas,
            ISourceCircuitBreakerRepository circuits,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(consent);
            ArgumentNullException.ThrowIfNull(quotas);
            ArgumentNullException.ThrowIfNull(circuits);
            ArgumentNullException.ThrowIfNull(clock);
            _sources = sources;
            _consent = consent;
            _quotas = quotas;
            _circuits = circuits;
            _clock = clock;
        }

        /// <summary>
        /// Returns every configured source's current status.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The sources ordered by key.</returns>
        public async Task<SourceListResponse> HandleAsync(CancellationToken cancellationToken)
        {
            var now = _clock.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var items = new List<SourceStatusResponse>();
            foreach (var settings in _sources.All)
            {
                var quota = await _quotas.FindAsync(settings.Source, today, cancellationToken);
                var circuit = await _circuits.FindAsync(settings.Source, cancellationToken);
                var gate = CompliancePolicy.CanRun(settings.Source, _consent);
                var limits = settings.DefaultLimits;

                items.Add(
                    new SourceStatusResponse(
                        settings.Source.Value,
                        settings.Source.Tier.ToString(),
                        settings.Enabled,
                        new SourceGate(gate.IsAllowed, gate.Reason),
                        new SourcePolicy(
                            limits.MaxPages,
                            limits.MaxJobs,
                            limits.MaxDetails,
                            (int)limits.MinDelay.TotalMilliseconds,
                            (int)limits.MaxDelay.TotalMilliseconds,
                            (int)settings.ChallengeCooldown.TotalMinutes
                        ),
                        new SourceQuotaUsage(
                            today.ToString(
                                "yyyy-MM-dd",
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            quota?.PagesFetched ?? 0,
                            settings.DailyPageCap,
                            quota?.DetailsFetched ?? 0,
                            settings.DailyDetailCap,
                            quota?.RunsStarted ?? 0,
                            settings.DailyRunCap,
                            quota?.Challenges ?? 0
                        ),
                        circuit is null
                            ? new SourceCircuit(nameof(CircuitState.Closed), null, false, 0, 0)
                            : new SourceCircuit(
                                circuit.State.ToString(),
                                circuit.OpenUntil,
                                circuit.CanProbe(now),
                                circuit.ConsecutiveFailures,
                                circuit.RecentFailureCount(now)
                            )
                    )
                );
            }

            return new SourceListResponse(items, now);
        }
    }
}
