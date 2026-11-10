using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Requires a log directory only when file logging is on
    /// (<see cref="FeaturesOptions.Logging"/>). With logging off no
    /// directory is used, so a missing <see cref="LoggingOptions.Directory"/>
    /// is not an error (docs/superpowers/specs/2026-11-08-feature-toggles-design.md
    /// §Logging toggle).
    /// </summary>
    public sealed class LoggingOptionsValidator : IValidateOptions<LoggingOptions>
    {
        // Injected and never mutated.
        private readonly FeaturesOptions _features;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptionsValidator"/> class.
        /// </summary>
        /// <param name="features">The feature flags that gate the directory requirement.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="features"/> is null.</exception>
        public LoggingOptionsValidator(IOptions<FeaturesOptions> features)
        {
            ArgumentNullException.ThrowIfNull(features);
            _features = features.Value;
        }

        /// <summary>
        /// Validates the bound logging options.
        /// </summary>
        /// <param name="name">The options instance name (unused).</param>
        /// <param name="options">The bound options.</param>
        /// <returns>Success, or a failure naming the missing directory.</returns>
        public ValidateOptionsResult Validate(string? name, LoggingOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (_features.Logging && string.IsNullOrWhiteSpace(options.Directory))
            {
                return ValidateOptionsResult.Fail(
                    $"{LoggingOptions.SectionName}:{nameof(LoggingOptions.Directory)} is required when Features:Logging is true; set JobScrape:Logging:Directory."
                );
            }

            return ValidateOptionsResult.Success;
        }
    }
}
