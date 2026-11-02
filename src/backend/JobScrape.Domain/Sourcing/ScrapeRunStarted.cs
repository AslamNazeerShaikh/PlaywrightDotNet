using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a run leaves <see cref="RunStage.Pending"/>.
    /// </summary>
    /// <param name="RunId">The started run.</param>
    /// <param name="ProfileId">The profile the run executes.</param>
    /// <param name="Source">The searched source.</param>
    /// <param name="OccurredAt">When the run started.</param>
    public sealed record ScrapeRunStarted(
        ScrapeRunId RunId,
        SearchProfileId ProfileId,
        SourceKey Source,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
