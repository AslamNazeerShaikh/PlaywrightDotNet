using JobScrape.Api.Composition;
using JobScrape.Api.Http;
using JobScrape.Api.Runs;
using JobScrape.Application.Features.Health;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Features.Sources;
using JobScrape.Infrastructure.Export;
using Serilog;

namespace JobScrape.Api
{
    /// <summary>
    /// Composition root. Wiring only; verified by the API smoke and
    /// integration tests rather than unit coverage.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class Program
    {
        /// <summary>
        /// Application entry point. Builds the host, wires services, and
        /// blocks serving requests until shutdown.
        /// </summary>
        /// <param name="args">Command-line arguments forwarded to the host builder.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers first: the API surface is controller-based so every
            // endpoint stays greppable and block-namespaced (no minimal-API
            // lambdas hidden in this method).
            builder.Services.AddControllers();

            // OpenAPI feeds Scalar locally and the cockpit codegen; served only
            // in Development (see the MapOpenApi guard below).
            builder.Services.AddOpenApi();

            // RFC 9457 problem details; every problem carries the request
            // path and a trace id (docs/14-application-patterns.md §6.4).
            builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                    context.ProblemDetails.Extensions.TryAdd(
                        "traceId",
                        System.Diagnostics.Activity.Current?.Id
                            ?? context.HttpContext.TraceIdentifier
                    );
                }
            );
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            // System clock as the ambient time source; handlers take
            // TimeProvider so tests can substitute a fake.
            builder.Services.AddSingleton(TimeProvider.System);

            // Bound and validated once; bad caps or consent flags fail
            // startup instead of running unsafely.
            builder.Services.AddJobScrapeOptions();

            // Serilog owns sinks and enrichers; app code logs through ILogger<T>.
            builder.Services.AddJobScrapeLogging(
                builder.Configuration,
                builder.Environment.ContentRootPath
            );

            // Explicit use-case handlers, registered one by one. No assembly
            // scanning, no mediator: "Find usages" on a handler finds its
            // callers.
            builder.Services.AddTransient<GetHealthHandler>();
            builder.Services.AddTransient<GetSearchProfileHandler>();
            builder.Services.AddTransient<ListSearchProfilesHandler>();
            builder.Services.AddTransient<CreateSearchProfileHandler>();
            builder.Services.AddTransient<ListSourcesHandler>();
            builder.Services.AddTransient<ListSessionsHandler>();
            builder.Services.AddTransient<GetSessionHandler>();
            builder.Services.AddTransient<CreateSessionHandler>();
            builder.Services.AddTransient<StartRunHandler>();
            builder.Services.AddTransient<ListRunsHandler>();
            builder.Services.AddTransient<GetRunHandler>();
            builder.Services.AddTransient<CancelRunHandler>();
            builder.Services.AddTransient<ListRunPostingsHandler>();
            builder.Services.AddTransient<ExportRunPostingsHandler>();
            builder.Services.AddSingleton<IPostingExportWriter, CsvPostingWriter>();
            builder.Services.AddSingleton<IPostingExportWriter, ExcelPostingWriter>();
            // The scrape itself: scoped, because it holds the run's unit of
            // work; the coordinator resolves it in a fresh scope per run.
            builder.Services.AddScoped<RunScrapeHandler>();

            // Runs outlive their request: one coordinator executes them in
            // the background, cancels on shutdown, and fails runs a crash
            // left mid-flight on the next start.
            builder.Services.AddSingleton<RunCoordinator>();
            builder.Services.AddSingleton<IRunLauncher>(provider =>
                provider.GetRequiredService<RunCoordinator>()
            );
            builder.Services.AddHostedService(provider =>
                provider.GetRequiredService<RunCoordinator>()
            );

            // Playwright, the session manager, wall artifacts, and adapters.
            builder.Services.AddJobScrapeBrowser(builder.Environment.ContentRootPath);

            // SQLite through EF Core: one scoped unit of work per request.
            builder.Services.AddJobScrapeDatabase(
                builder.Configuration,
                builder.Environment.ContentRootPath
            );

            var app = builder.Build();

            // Development-only document endpoint; never proxied by the
            // cockpit BFF and never exposed beyond loopback.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            DatabaseRegistration.MigrateInDevelopment(app);

            app.UseMiddleware<CorrelationMiddleware>();
            app.UseExceptionHandler();
            // One completion event per request, written through this host's
            // logger rather than the static Log.Logger.
            app.UseSerilogRequestLogging(options =>
                options.Logger = app.Services.GetRequiredService<Serilog.ILogger>()
            );
            app.MapControllers();

            // Run disposes the host on every exit path; that disposes the
            // container-owned Serilog logger and flushes the async file sink
            // (docs/15-runtime-hardening.md §2.9).
            app.Run();
        }
    }
}
