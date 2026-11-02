using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a refresh changes a posting's content hash. A re-sighting
    /// with unchanged content raises nothing.
    /// </summary>
    /// <param name="PostingId">The changed posting.</param>
    /// <param name="Source">The posting's source.</param>
    /// <param name="OldHash">The hash before the refresh.</param>
    /// <param name="NewHash">The hash after the refresh.</param>
    /// <param name="OccurredAt">When the change was observed.</param>
    public sealed record JobPostingUpdated(
        JobPostingId PostingId,
        SourceKey Source,
        ContentHash OldHash,
        ContentHash NewHash,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
