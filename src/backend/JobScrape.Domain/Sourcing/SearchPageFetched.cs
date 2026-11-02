using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a result page is parsed and recorded.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="PageNumber">One-based page number within the run.</param>
    /// <param name="JobCount">Valid postings parsed from the page.</param>
    /// <param name="OccurredAt">When the page was recorded.</param>
    public sealed record SearchPageFetched(
        ScrapeRunId RunId,
        int PageNumber,
        int JobCount,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
