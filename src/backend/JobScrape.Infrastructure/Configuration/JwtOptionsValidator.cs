using System.Text;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Rejects bearer secrets that cannot sign safely. The shipped
    /// appsettings value is a dev-only secret that boots; the
    /// <see cref="DemoPlaceholder"/> constant is still refused if
    /// configured, and any secret under 32 UTF-8 bytes (byte count, not
    /// character count) is rejected so multi-byte secrets cannot slip under
    /// the HS256 floor. The secret is never logged.
    /// </summary>
    public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        /// <summary>
        /// The demo placeholder. Still refused if configured, even though
        /// the shipped appsettings now carries a dev-only secret that boots.
        /// Never logged.
        /// </summary>
        public const string DemoPlaceholder = "CHANGE-ME-TO-A-SECRET-OF-AT-LEAST-32-CHARS";

        /// <summary>
        /// The minimum secret size in UTF-8 bytes.
        /// </summary>
        internal const int MinSecretBytes = 32;

        /// <summary>
        /// Validates the bound JWT options.
        /// </summary>
        /// <param name="name">The options instance name (unused).</param>
        /// <param name="options">The bound options.</param>
        /// <returns>Success, or a failure naming the unsafe secret.</returns>
        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.Secret == DemoPlaceholder)
            {
                return ValidateOptionsResult.Fail(
                    $"{JwtOptions.SectionName}:{nameof(JwtOptions.Secret)} still holds the demo placeholder; set the JobScrape:Jwt:Secret configuration value (JobScrape__Jwt__Secret environment variable)."
                );
            }

            if (Encoding.UTF8.GetByteCount(options.Secret) < MinSecretBytes)
            {
                return ValidateOptionsResult.Fail(
                    $"{JwtOptions.SectionName}:{nameof(JwtOptions.Secret)} must be at least {MinSecretBytes} UTF-8 bytes."
                );
            }

            return ValidateOptionsResult.Success;
        }
    }
}
