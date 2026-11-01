using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raised when a wall pauses a run for manual intervention.
    /// </summary>
    /// <param name="RunId">The paused run.</param>
    /// <param name="Kind">The wall that paused it.</param>
    /// <param name="OccurredAt">When the run paused.</param>
    public sealed record RunPausedForChallenge(
        ScrapeRunId RunId,
        ChallengeKind Kind,
        DateTimeOffset OccurredAt
    ) : DomainEvent(OccurredAt);
}
