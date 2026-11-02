using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Raised when a source's circuit opens.
    /// </summary>
    /// <param name="Source">The blocked source.</param>
    /// <param name="Trigger">The wall that opened it.</param>
    /// <param name="OpenUntil">When a probe may be attempted.</param>
    /// <param name="OccurredAt">When the circuit opened.</param>
    public sealed record CircuitOpened(
        SourceKey Source,
        ChallengeKind Trigger,
        DateTimeOffset OpenUntil,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
