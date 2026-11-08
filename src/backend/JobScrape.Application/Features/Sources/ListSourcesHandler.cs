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

    /// <summary>
    /// Whether runs against the source can pass the compliance gate.
    /// </summary>
    /// <param name="Allowed">True when the configured consent satisfies the tier.</param>
    /// <param name="Reason">Operator guidance naming what is missing; null when allowed.</param>
    public sealed record SourceGate(bool Allowed, string? Reason);

    /// <summary>
    /// The source's policy limits: caps new profiles start with and may only tighten.
    /// </summary>
    /// <param name="MaxPages">Result pages per run.</param>
    /// <param name="MaxJobs">Postings per run.</param>
    /// <param name="MaxDetails">Detail visits per run.</param>
    /// <param name="MinDelayMs">Shortest delay between requests.</param>
    /// <param name="MaxDelayMs">Longest delay between requests.</param>
    /// <param name="ChallengeCooldownMinutes">Profile cooldown after a wall.</param>
    public sealed record SourcePolicy(
        int MaxPages,
        int MaxJobs,
        int MaxDetails,
        int MinDelayMs,
        int MaxDelayMs,
        int ChallengeCooldownMinutes
    );

    /// <summary>
    /// Today's (UTC) quota usage; zero when no run has touched the day.
    /// </summary>
    /// <param name="Date">The UTC day, <c>yyyy-MM-dd</c>.</param>
    /// <param name="PagesUsed">Result pages fetched today.</param>
    /// <param name="PageCap">Daily page cap.</param>
    /// <param name="DetailsUsed">Detail pages visited today.</param>
    /// <param name="DetailCap">Daily detail cap.</param>
    /// <param name="RunsUsed">Runs started today.</param>
    /// <param name="RunCap">Daily run cap, or null for no run cap.</param>
    /// <param name="Challenges">Walls hit today.</param>
    public sealed record SourceQuotaUsage(
        string Date,
        int PagesUsed,
        int PageCap,
        int DetailsUsed,
        int DetailCap,
        int RunsUsed,
        int? RunCap,
        int Challenges
    );

    /// <summary>
    /// The source's circuit breaker; <c>Closed</c> with no failures when it never tripped.
    /// </summary>
    /// <param name="State"><c>Closed</c>, <c>Open</c>, or <c>HalfOpen</c>.</param>
    /// <param name="OpenUntil">When a probe may be attempted, while open.</param>
    /// <param name="CanProbe">True when open and the cooldown has elapsed.</param>
    /// <param name="ConsecutiveFailures">Walls in a row since the last success.</param>
    /// <param name="RecentFailures">Walls inside the 24-hour window.</param>
    public sealed record SourceCircuit(
        string State,
        DateTimeOffset? OpenUntil,
        bool CanProbe,
        int ConsecutiveFailures,
        int RecentFailures
    );
}
