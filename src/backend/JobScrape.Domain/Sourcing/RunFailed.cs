using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a run stops on an unrecoverable failure.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Reason">Operator-safe failure reason; never HTML, cookies, or paths.</param>
    /// <param name="Stats">The counters at failure.</param>
    /// <param name="OccurredAt">When the run failed.</param>
    public sealed record RunFailed(
        ScrapeRunId RunId,
        string Reason,
        RunStats Stats,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
