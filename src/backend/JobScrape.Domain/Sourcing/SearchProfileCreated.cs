using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a search profile is created.
    /// </summary>
    /// <param name="ProfileId">The new profile.</param>
    /// <param name="Source">The source the profile searches.</param>
    /// <param name="OccurredAt">When the profile was created.</param>
    public sealed record SearchProfileCreated(
        SearchProfileId ProfileId,
        SourceKey Source,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
