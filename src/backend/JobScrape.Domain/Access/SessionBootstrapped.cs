using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Raised when a successful probe verifies a profile's session.
    /// </summary>
    /// <param name="ProfileId">The verified browser profile.</param>
    /// <param name="Source">The profile's source.</param>
    /// <param name="State">The state after verification.</param>
    /// <param name="OccurredAt">When the session was verified.</param>
    public sealed record SessionBootstrapped(
        BrowserProfileId ProfileId,
        SourceKey Source,
        SessionState State,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
