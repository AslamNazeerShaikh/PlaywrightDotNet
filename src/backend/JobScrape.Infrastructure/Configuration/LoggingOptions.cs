using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Log file location and retention (docs/15-runtime-hardening.md §2.3, §2.8).
    /// </summary>
    public sealed class LoggingOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Logging";

        /// <summary>
        /// Gets the directory for rolling JSON log files.
        /// </summary>
        [Required]
        public string Directory { get; init; } = "artifacts/logs";

        /// <summary>
        /// Gets how long log files are kept, in days.
        /// </summary>
        [Range(1, 3650)]
        public int RetentionDays { get; init; } = 90;

        /// <summary>
        /// Gets the size at which a log file rolls over, in bytes (1 MB to 1 GB).
        /// </summary>
        [Range(1L << 20, 1L << 30)]
        public long FileSizeLimitBytes { get; init; } = 100L << 20;
    }
}
