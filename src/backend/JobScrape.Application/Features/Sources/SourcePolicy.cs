namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// The source's policy limits: caps new profiles start with and may only tighten.
    /// </summary>
    /// <param name="MaxPages">Result pages per run.</param>
    /// <param name="MaxJobs">Postings per run.</param>
    /// <param name="MaxDetails">Detail visits per run.</param>
    /// <param name="MinDelayMs">Shortest delay between requests.</param>
    /// <param name="MaxDelayMs">Longest delay between requests.</param>
    /// <param name="ChallengeCooldownMinutes">Profile cooldown after a wall.</param>
    public sealed record SourcePolicy(
        int MaxPages,
        int MaxJobs,
        int MaxDetails,
        int MinDelayMs,
        int MaxDelayMs,
        int ChallengeCooldownMinutes
    );
}
