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
        /// Gets the directory for rolling JSON log files. Required while
        /// logging is on (<see cref="LoggingOptionsValidator"/> enforces it);
        /// skipped when the Logging feature flag is off.
        /// </summary>
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

        /// <summary>
        /// Gets how often the human-readable log rolls over: <c>Hour</c> (the default) or <c>Day</c>,
        /// matched exactly (case-sensitive).
        /// </summary>
        [Required]
        [RegularExpression(
            "^(Hour|Day)$",
            ErrorMessage = "Logging:PlainTextRollingInterval must be 'Hour' or 'Day'."
        )]
        public string PlainTextRollingInterval { get; init; } = "Hour";

        /// <summary>
        /// Gets how long human-readable log files are kept, in days.
        /// </summary>
        [Range(1, 3650)]
        public int PlainTextRetentionDays { get; init; } = 14;

        /// <summary>
        /// Gets the size at which a human-readable log file rolls over, in bytes (1 MB to 1 GB).
        /// </summary>
        [Range(1L << 20, 1L << 30)]
        public long PlainTextFileSizeLimitBytes { get; init; } = 100L << 20;
    }
}
