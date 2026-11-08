namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// First-boot admin seed credentials. Bound once at the composition
    /// root from the <c>JobScrape:AdminSeed</c> section; every value is
    /// optional and a missing or empty section skips seeding. Hierarchical
    /// environment variables (<c>JobScrape__AdminSeed__Email</c>,
    /// <c>JobScrape__AdminSeed__SetupSecret</c>) override the file. The
    /// legacy <c>ADMIN_EMAIL</c> / <c>ADMIN_SETUP_SECRET</c> names apply
    /// only when the <c>JobScrape:AdminSeed</c> section values are absent.
    /// Secrets are never logged.
    /// </summary>
    public sealed class AdminSeedOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:AdminSeed";

        /// <summary>
        /// Gets the seed admin email. Null or blank skips seeding.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// Gets the seed admin setup secret. Must meet the password
        /// criteria; null or empty skips seeding.
        /// </summary>
        public string? SetupSecret { get; init; }
    }
}
