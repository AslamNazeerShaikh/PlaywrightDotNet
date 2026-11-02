using System.ComponentModel.DataAnnotations;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Cross-field rules for per-source options that DataAnnotations cannot
    /// express. Safety caps never fall back to defaults: any violation fails
    /// host startup (docs/02-architecture.md §11.2).
    /// </summary>
    public sealed class SourcesOptionsValidator : IValidateOptions<SourcesOptions>
    {
        // Injected and never mutated.
        private readonly ComplianceOptions _compliance;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourcesOptionsValidator"/> class.
        /// </summary>
        /// <param name="compliance">The compliance flags that gate enabling risky sources.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="compliance"/> is null.</exception>
        public SourcesOptionsValidator(IOptions<ComplianceOptions> compliance)
        {
            ArgumentNullException.ThrowIfNull(compliance);
            _compliance = compliance.Value;
        }

        /// <summary>
        /// Validates every configured source.
        /// </summary>
        /// <param name="name">The options instance name (unused).</param>
        /// <param name="options">The bound options.</param>
        /// <returns>Success, or a failure listing every violation.</returns>
        public ValidateOptionsResult Validate(string? name, SourcesOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var failures = new List<string>();
            foreach (var (key, source) in options.Sources)
            {
                if (!SourceKey.TryParse(key, out var sourceKey))
                {
                    failures.Add($"Sources:{key}: unknown source.");
                    continue;
                }

                var annotations = new List<ValidationResult>();
                if (
                    !Validator.TryValidateObject(
                        source,
                        new ValidationContext(source),
                        annotations,
                        true
                    )
                )
                {
                    failures.AddRange(
                        annotations.Select(result => $"Sources:{key}: {result.ErrorMessage}")
                    );
                    continue;
                }

                if (source.UnlimitedRunCap && source.DailyRunCap > 0)
                {
                    failures.Add(
                        $"Sources:{key}: UnlimitedRunCap=true conflicts with DailyRunCap={source.DailyRunCap}; remove one."
                    );
                    continue;
                }

                // RunLimits is the single source of truth for cap and delay ranges.
                try
                {
                    _ = ToRunLimits(source);
                }
                catch (DomainException ex)
                {
                    failures.Add($"Sources:{key}: {ex.Message}");
                }

                if (
                    source.Enabled
                    && sourceKey.Tier != ComplianceTier.Standard
                    && !_compliance.AcknowledgeSourceRisk
                )
                {
                    failures.Add(
                        $"Sources:{key}: enabling a {sourceKey.Tier} source requires Compliance:AcknowledgeSourceRisk=true."
                    );
                }

                if (
                    source.Enabled
                    && sourceKey.Tier == ComplianceTier.HighRisk
                    && !_compliance.AllowHighRiskSources
                )
                {
                    failures.Add(
                        $"Sources:{key}: enabling a HighRisk source requires Compliance:AllowHighRiskSources=true."
                    );
                }
            }

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        /// <summary>
        /// Builds the domain caps a source's options describe.
        /// </summary>
        /// <param name="source">The source options.</param>
        /// <returns>The run limits.</returns>
        /// <exception cref="DomainException">Thrown when the options describe invalid limits.</exception>
        public static RunLimits ToRunLimits(SourceOptions source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new RunLimits(
                source.MaxPagesPerSearch,
                source.MaxJobsPerRun,
                source.MaxDetailsPerRun,
                TimeSpan.FromMilliseconds(source.MinDelayMs),
                TimeSpan.FromMilliseconds(source.MaxDelayMs)
            );
        }
    }
}
