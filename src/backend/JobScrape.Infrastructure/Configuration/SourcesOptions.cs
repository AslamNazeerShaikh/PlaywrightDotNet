namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Per-source options keyed by source key (<c>naukri</c>, <c>indeed</c>,
    /// <c>linkedin</c>), bound from <c>JobScrape:Sources</c>.
    /// </summary>
    public sealed class SourcesOptions
    {
        /// <summary>
        /// The configuration section that contains <see cref="Sources"/>.
        /// </summary>
        public const string SectionName = "JobScrape";

        /// <summary>
        /// Gets the per-source options.
        /// </summary>
        public Dictionary<string, SourceOptions> Sources { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
