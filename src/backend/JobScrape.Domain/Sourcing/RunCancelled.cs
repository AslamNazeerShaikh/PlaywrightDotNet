using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a run is cancelled by the operator; partial results are kept.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Stats">The final counters.</param>
    /// <param name="OccurredAt">When the run ended.</param>
    public sealed record RunCancelled(ScrapeRunId RunId, RunStats Stats, DateTimeOffset OccurredAt)
        : DomainEvent(OccurredAt);
}
