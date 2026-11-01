using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Per-source operating limits, bound and validated from configuration at
    /// the composition root (docs/04-solution-and-tooling.md §5.1) and handed
    /// to the application as a plain record. Caps are starting defaults, not
    /// entitlements.
    /// </summary>
    /// <param name="Source">The source.</param>
    /// <param name="Enabled">False blocks every run against the source.</param>
    /// <param name="DailyPageCap">Result pages per UTC day.</param>
    /// <param name="DailyDetailCap">Detail pages per UTC day.</param>
    /// <param name="DailyRunCap">Runs started per UTC day.</param>
    /// <param name="ChallengeCooldown">How long a profile cools down after a wall.</param>
    public sealed record SourceSettings(
        SourceKey Source,
        bool Enabled,
        int DailyPageCap,
        int DailyDetailCap,
        int DailyRunCap,
        TimeSpan ChallengeCooldown
    );
}
