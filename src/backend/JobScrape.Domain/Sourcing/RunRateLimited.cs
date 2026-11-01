using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when the source throttles a run.
    /// </summary>
    /// <param name="RunId">The throttled run.</param>
    /// <param name="RetryAt">The earliest time the run may resume.</param>
    /// <param name="OccurredAt">When the throttle was recorded.</param>
    public sealed record RunRateLimited(
        ScrapeRunId RunId,
        DateTimeOffset RetryAt,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
