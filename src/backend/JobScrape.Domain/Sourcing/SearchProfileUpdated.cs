using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a profile's criteria, limits, or enabled state actually
    /// changes. A no-op update raises nothing.
    /// </summary>
    /// <param name="ProfileId">The changed profile.</param>
    /// <param name="ChangedField">
    /// The changed field: <c>criteria</c>, <c>limits</c>, or <c>enabled</c>.
    /// </param>
    /// <param name="OccurredAt">When the change happened.</param>
    public sealed record SearchProfileUpdated(
        SearchProfileId ProfileId,
        string ChangedField,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
