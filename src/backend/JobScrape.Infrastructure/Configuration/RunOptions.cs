using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Run-wide settings.
    /// </summary>
    public sealed class RunOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Run";

        /// <summary>
        /// Gets the total attempts for a transient failure, including the first.
        /// </summary>
        [Range(1, 3)]
        public int RetryMaxAttempts { get; init; } = 2;
    }
}
