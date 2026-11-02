using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Where local data lives and how long it is kept
    /// (docs/04-solution-and-tooling.md §5.1). Relative paths resolve
    /// against the content root.
    /// </summary>
    public sealed class StorageOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Storage";

        /// <summary>
        /// Gets the SQLite database file path.
        /// </summary>
        [Required]
        public string DatabasePath { get; init; } = "artifacts/data/jobscrape.db";

        /// <summary>
        /// Gets the root for run artifacts (screenshots, HTML, traces).
        /// </summary>
        [Required]
        public string ArtifactsRoot { get; init; } = "artifacts";

        /// <summary>
        /// Gets the root for persistent browser profiles.
        /// </summary>
        [Required]
        public string ProfilesRoot { get; init; } = "artifacts/profiles";

        /// <summary>
        /// Gets how long run artifacts are kept, in days.
        /// </summary>
        [Range(1, 3650)]
        public int RetentionDays { get; init; } = 30;

        /// <summary>
        /// Gets how long a posting is kept after it was last seen, in days.
        /// </summary>
        [Range(1, 3650)]
        public int PostingRetentionDays { get; init; } = 180;

        /// <summary>
        /// Gets the free-space floor; writes stop below it.
        /// </summary>
        [Range(1L, long.MaxValue)]
        public long MinimumFreeDiskBytes { get; init; } = 1L << 30;
    }
}
