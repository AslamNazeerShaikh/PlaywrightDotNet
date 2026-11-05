using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Composition
{
    /// <summary>
    /// Binds and validates every <c>JobScrape:*</c> section once, at startup
    /// (docs/14-application-patterns.md §4). Invalid configuration fails the
    /// host instead of running with unsafe caps. The application layer gets
    /// plain values (<see cref="SourceSettingsCatalog"/>,
    /// <see cref="RetryPolicy"/>), never <c>IOptions</c>.
    /// </summary>
    public static class OptionsRegistration
    {
        /// <summary>
        /// Registers the validated options and the values derived from them.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddJobScrapeOptions(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services
                .AddOptions<StorageOptions>()
                .BindConfiguration(StorageOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services
                .AddOptions<ComplianceOptions>()
                .BindConfiguration(ComplianceOptions.SectionName)
                .ValidateOnStart();
            services
                .AddOptions<RunOptions>()
                .BindConfiguration(RunOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services
                .AddOptions<LoggingOptions>()
                .BindConfiguration(LoggingOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services
                .AddOptions<SourcesOptions>()
                .BindConfiguration(SourcesOptions.SectionName)
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<SourcesOptions>, SourcesOptionsValidator>();

            services.AddSingleton(provider =>
                SourceSettingsMapper.ToCatalog(
                    provider.GetRequiredService<IOptions<SourcesOptions>>().Value
                )
            );
            // Configured consent only: interactive confirmation is a per-run
            // operator act, so it is never true in configuration.
            services.AddSingleton(provider =>
            {
                var compliance = provider.GetRequiredService<IOptions<ComplianceOptions>>().Value;
                return new ComplianceConsent(
                    compliance.AcknowledgeSourceRisk,
                    compliance.AllowHighRiskSources,
                    InteractiveConfirmation: false
                );
            });
            services.AddSingleton<IRandomSource, SystemRandomSource>();
            services.AddSingleton<IDelay, TimeProviderDelay>();
            services.AddSingleton(provider => new RetryPolicy(
                provider.GetRequiredService<IOptions<RunOptions>>().Value.RetryMaxAttempts,
                provider.GetRequiredService<IRandomSource>()
            ));
            return services;
        }
    }
}
