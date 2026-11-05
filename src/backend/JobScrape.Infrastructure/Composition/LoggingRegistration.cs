using JobScrape.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

namespace JobScrape.Infrastructure.Composition
{
    /// <summary>
    /// Serilog setup for the API (docs/15-runtime-hardening.md §2): the
    /// <c>Serilog</c> section for levels, the standard enricher set, rendered
    /// compact JSON on the console, and an async, rolling compact-JSON file
    /// sink with retention from the validated <see cref="LoggingOptions"/>.
    /// </summary>
    public static class LoggingRegistration
    {
        /// <summary>
        /// The <c>Application</c> property stamped on every event.
        /// </summary>
        public const string ApplicationName = "JobScrape.Api";

        /// <summary>
        /// Registers Serilog as the host's logger.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The app configuration (levels and overrides).</param>
        /// <param name="contentRoot">Root for a relative log directory.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IServiceCollection AddJobScrapeLogging(
            this IServiceCollection services,
            IConfiguration configuration,
            string contentRoot
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(contentRoot);

            // Resolving the options here makes an invalid Logging section
            // fail startup like every other validated section. The static
            // Log.Logger is preserved: the logger is owned by this host's
            // container (no process-wide mutable state), and disposing the
            // host disposes it, which flushes the async file sink.
            return services.AddSerilog(
                (provider, logger) =>
                {
                    var options = provider.GetRequiredService<IOptions<LoggingOptions>>().Value;
                    logger
                        .ReadFrom.Configuration(configuration)
                        .ReadFrom.Services(provider)
                        .Enrich.FromLogContext()
                        .Enrich.WithMachineName()
                        .Enrich.WithEnvironmentName()
                        .Enrich.WithProcessId()
                        .Enrich.WithProcessName()
                        .Enrich.WithThreadId()
                        .Enrich.WithSpan()
                        .Enrich.WithAssemblyName()
                        .Enrich.WithAssemblyVersion()
                        .Enrich.WithExceptionDetails()
                        .Enrich.WithDemystifiedStackTraces()
                        .Enrich.WithProperty("Application", ApplicationName)
                        .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .WriteTo.Async(
                            sink =>
                                sink.File(
                                    new CompactJsonFormatter(),
                                    Path.Combine(
                                        Path.GetFullPath(options.Directory, contentRoot),
                                        "jobscrape-api-.jsonl"
                                    ),
                                    fileSizeLimitBytes: options.FileSizeLimitBytes,
                                    rollingInterval: RollingInterval.Day,
                                    rollOnFileSizeLimit: true,
                                    retainedFileCountLimit: 200,
                                    retainedFileTimeLimit: TimeSpan.FromDays(options.RetentionDays)
                                ),
                            bufferSize: 10_000,
                            blockWhenFull: true
                        );
                },
                preserveStaticLogger: true
            );
        }
    }
}
