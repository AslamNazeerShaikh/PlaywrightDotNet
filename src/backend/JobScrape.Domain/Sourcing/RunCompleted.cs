using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a run finishes successfully.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Stats">The final counters.</param>
    /// <param name="OccurredAt">When the run ended.</param>
    public sealed record RunCompleted(ScrapeRunId RunId, RunStats Stats, DateTimeOffset OccurredAt)
        : DomainEvent(OccurredAt);
}
