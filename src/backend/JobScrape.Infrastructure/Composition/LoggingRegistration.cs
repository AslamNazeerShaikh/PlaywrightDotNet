using System.Globalization;
using JobScrape.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Configuration;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

namespace JobScrape.Infrastructure.Composition
{
    /// <summary>
    /// Serilog setup both hosts share (docs/15-runtime-hardening.md §2):
    /// the <c>Serilog</c> section for levels, the standard enricher set,
    /// rendered compact JSON on the console, an async, rolling
    /// compact-JSON <c>.jsonl</c> file sink, and an async, rolling
    /// human-readable <c>.log</c> file sink, both with retention from the
    /// validated <see cref="LoggingOptions"/>.
    /// </summary>
    public static class LoggingRegistration
    {
        /// <summary>
        /// The default <c>Application</c> property stamped on every event.
        /// </summary>
        public const string ApplicationName = "JobScrape.Api";

        private const string PlainTextOutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        private static void AddAsyncFileSink(
            LoggerConfiguration logger,
            Action<LoggerSinkConfiguration> addSink
        ) => logger.WriteTo.Async(addSink, bufferSize: 10_000, blockWhenFull: true);

        /// <summary>
        /// Registers Serilog as the host's logger.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The app configuration (levels and overrides).</param>
        /// <param name="contentRoot">Root for a relative log directory.</param>
        /// <param name="applicationName">
        /// The <c>Application</c> property stamped on every event. Defaults
        /// to <see cref="ApplicationName"/>.
        /// </param>
        /// <param name="logFilePrefix">
        /// The rolling file name prefix (<c>{prefix}-.jsonl</c> and
        /// <c>{prefix}-.log</c>). Defaults to <c>jobscrape-api</c>.
        /// </param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IServiceCollection AddJobScrapeLogging(
            this IServiceCollection services,
            IConfiguration configuration,
            string contentRoot,
            string? applicationName = null,
            string? logFilePrefix = null
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(contentRoot);

            var application = applicationName ?? ApplicationName;
            var prefix = logFilePrefix ?? "jobscrape-api";

            // Resolving the options here makes an invalid Logging section
            // fail startup like every other validated section. The static
            // Log.Logger is preserved: the logger is owned by this host's
            // container (no process-wide mutable state), and disposing the
            // host disposes it, which flushes the async file sink.
            return services.AddSerilog(
                (provider, logger) =>
                {
                    var options = provider.GetRequiredService<IOptions<LoggingOptions>>().Value;
                    var directory = Path.GetFullPath(options.Directory, contentRoot);
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
                        .Enrich.WithProperty("Application", application)
                        .WriteTo.Console(new RenderedCompactJsonFormatter());
                    AddAsyncFileSink(
                        logger,
                        sink =>
                            sink.File(
                                new CompactJsonFormatter(),
                                Path.Combine(directory, $"{prefix}-.jsonl"),
                                fileSizeLimitBytes: options.FileSizeLimitBytes,
                                rollingInterval: RollingInterval.Day,
                                rollOnFileSizeLimit: true,
                                retainedFileCountLimit: 200,
                                retainedFileTimeLimit: TimeSpan.FromDays(options.RetentionDays)
                            )
                    );
                    AddAsyncFileSink(
                        logger,
                        sink =>
                            sink.File(
                                Path.Combine(directory, $"{prefix}-.log"),
                                outputTemplate: PlainTextOutputTemplate,
                                formatProvider: CultureInfo.InvariantCulture,
                                fileSizeLimitBytes: options.PlainTextFileSizeLimitBytes,
                                rollingInterval: options.PlainTextRollingInterval switch
                                {
                                    "Hour" => RollingInterval.Hour,
                                    "Day" => RollingInterval.Day,
                                    // Reachable only if validation is bypassed (Options.Create in
                                    // tests) or the logger builds before ValidateOnStart; fail closed.
                                    _ => throw new InvalidOperationException(
                                        $"Unsupported PlainTextRollingInterval '{options.PlainTextRollingInterval}'."
                                    ),
                                },
                                rollOnFileSizeLimit: true,
                                retainedFileCountLimit: 336,
                                retainedFileTimeLimit: TimeSpan.FromDays(
                                    options.PlainTextRetentionDays
                                )
                            )
                    );
                },
                preserveStaticLogger: true
            );
        }
    }
}
