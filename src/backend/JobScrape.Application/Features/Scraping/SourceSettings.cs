using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

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
    /// <param name="DailyRunCap">Runs started per UTC day, or null for no run cap.</param>
    /// <param name="MaxPagesCeiling">Highest page cap a profile may request.</param>
    /// <param name="MaxJobsCeiling">Highest job cap a profile may request.</param>
    /// <param name="ChallengeCooldown">How long a profile cools down after a wall.</param>
    /// <param name="DefaultLimits">
    /// The policy caps and delay window for this source: new profiles start
    /// with them, and a profile may only tighten them, never loosen them.
    /// </param>
    public sealed record SourceSettings(
        SourceKey Source,
        bool Enabled,
        int DailyPageCap,
        int DailyDetailCap,
        int? DailyRunCap,
        int MaxPagesCeiling,
        int MaxJobsCeiling,
        TimeSpan ChallengeCooldown,
        RunLimits DefaultLimits
    );
}
