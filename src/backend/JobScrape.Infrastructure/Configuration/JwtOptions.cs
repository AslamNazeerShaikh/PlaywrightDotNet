using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// HS256 bearer settings plus account lockout policy. Bound once at the
    /// composition root and validated on startup. The secret comes from the
    /// <c>JobScrape:Jwt:Secret</c> configuration value (appsettings carries
    /// a dev-only secret that boots; secret store via CI/CD in production).
    /// The <c>DemoPlaceholder</c> constant is still refused if configured.
    /// The secret is never logged.
    /// </summary>
    public sealed class JwtOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Jwt";

        /// <summary>
        /// Gets the HS256 signing secret. At least 32 characters.
        /// </summary>
        [Required]
        [MinLength(32)]
        public string Secret { get; init; } = string.Empty;

        /// <summary>
        /// Gets the token issuer claim.
        /// </summary>
        [Required]
        public string Issuer { get; init; } = "jobscrape";

        /// <summary>
        /// Gets the token audience claim.
        /// </summary>
        [Required]
        public string Audience { get; init; } = "jobscrape-api";

        /// <summary>
        /// Gets how long an issued token stays valid, in minutes.
        /// </summary>
        [Range(1, 1440)]
        public int TokenLifetimeMinutes { get; init; } = 60;

        /// <summary>
        /// Gets the consecutive failures that lock an account.
        /// </summary>
        [Range(1, 100)]
        public int MaxFailedAttempts { get; init; } = 5;

        /// <summary>
        /// Gets how long a lockout lasts, in minutes.
        /// </summary>
        [Range(1, 1440)]
        public int LockoutMinutes { get; init; } = 15;
    }
}
