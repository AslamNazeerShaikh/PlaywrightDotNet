using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// One source's caps and pacing. These are starting defaults, not
    /// entitlements: lower them when a source challenges, never raise them
    /// without a review. Ranges mirror the domain's <c>RunLimits</c>.
    /// </summary>
    public sealed class SourceOptions
    {
        /// <summary>
        /// Gets a value indicating whether runs against the source may start.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// Gets the source's base URL.
        /// </summary>
        [Required]
        [Url]
        public string BaseUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets the default result pages per search.
        /// </summary>
        [Range(1, 30)]
        public int MaxPagesPerSearch { get; init; } = 1;

        /// <summary>
        /// Gets the default postings per run.
        /// </summary>
        [Range(1, 1000)]
        public int MaxJobsPerRun { get; init; } = 1;

        /// <summary>
        /// Gets the default detail visits per run; 0 disables enrichment.
        /// </summary>
        [Range(0, 1000)]
        public int MaxDetailsPerRun { get; init; }

        /// <summary>
        /// Gets the shortest delay between requests, in milliseconds.
        /// </summary>
        [Range(1000, 60_000)]
        public int MinDelayMs { get; init; } = 1000;

        /// <summary>
        /// Gets the longest delay between requests, in milliseconds.
        /// </summary>
        [Range(1000, 120_000)]
        public int MaxDelayMs { get; init; } = 1000;

        /// <summary>
        /// Gets the result pages allowed per UTC day.
        /// </summary>
        [Range(0, 10_000)]
        public int DailyPageCap { get; init; }

        /// <summary>
        /// Gets the detail visits allowed per UTC day.
        /// </summary>
        [Range(0, 10_000)]
        public int DailyDetailCap { get; init; }

        /// <summary>
        /// Gets the runs allowed per UTC day. Zero blocks all runs. Ignored
        /// when <see cref="UnlimitedRunCap"/> is true.
        /// </summary>
        [Range(0, 100)]
        public int DailyRunCap { get; init; }

        /// <summary>
        /// Gets a value indicating whether the source has no daily run cap.
        /// Defaults to false so an omitted or mistyped cap stays fail-closed.
        /// </summary>
        public bool UnlimitedRunCap { get; init; }

        /// <summary>
        /// Gets how long a browser profile cools down after a wall, in minutes.
        /// </summary>
        [Range(1, 10_080)]
        public int ChallengeCooldownMinutes { get; init; } = 60;
    }
}
