using JobScrape.Api.Composition;
using JobScrape.Api.Runs;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Features.Sources;
using JobScrape.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace JobScrape.Mcp.Host
{
    /// <summary>
    /// The stdio composition root. Same options, stores, and handlers as
    /// the API; the only differences are the transport (stdio, so logs go
    /// to stderr and never touch stdout) and the caller (always the local
    /// operator, since stdio carries no transport identity).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class Program
    {
        /// <summary>
        /// Builds the host, starts the stdio MCP server, and blocks until
        /// stdin closes or the process is stopped.
        /// </summary>
        /// <param name="args">Command-line arguments forwarded to the host builder.</param>
        /// <returns>A task that completes when the host shuts down.</returns>
        public static async Task Main(string[] args)
        {
            // Stdout is the JSON-RPC channel; everything log-shaped goes to
            // stderr so framing on stdout stays intact.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                    standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose
                )
                .CreateLogger();

            try
            {
                var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
                builder.Services.AddSerilog();

                // System clock as the ambient time source; handlers take
                // TimeProvider so tests can substitute a fake.
                builder.Services.AddSingleton(TimeProvider.System);

                // Bound and validated once; bad caps or consent flags fail
                // startup instead of running unsafely.
                builder.Services.AddJobScrapeOptions();

                // Explicit use-case handlers, registered one by one. No
                // assembly scanning, no mediator: "Find usages" on a handler
                // finds its callers. Mirrors JobScrape.Api Program.cs.
                builder.Services.AddTransient<GetHealthHandler>();
                builder.Services.AddTransient<GetSearchProfileHandler>();
                builder.Services.AddTransient<ListSearchProfilesHandler>();
                builder.Services.AddTransient<CreateSearchProfileHandler>();
                builder.Services.AddTransient<UpdateSearchProfileHandler>();
                builder.Services.AddTransient<ListSourcesHandler>();
                builder.Services.AddTransient<ListSessionsHandler>();
                builder.Services.AddTransient<GetSessionHandler>();
                builder.Services.AddTransient<CreateSessionHandler>();
                builder.Services.AddTransient<UpdateSessionHandler>();
                builder.Services.AddTransient<StartRunHandler>();
                builder.Services.AddTransient<ListRunsHandler>();
                builder.Services.AddTransient<GetRunHandler>();
                builder.Services.AddTransient<CancelRunHandler>();
                builder.Services.AddTransient<ListRunPostingsHandler>();
                // The scrape itself: scoped, because it holds the run's unit
                // of work; the coordinator resolves it in a fresh scope per run.
                builder.Services.AddScoped<RunScrapeHandler>();

                // Runs outlive their request: one coordinator executes them
                // in the background and cancels on host shutdown.
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

                // Stdio carries no transport identity; every call runs as
                // the local operator.
                builder.Services.AddScoped<ICallerContext>(_ => CallerContext.LocalOperator);

                builder
                    .Services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithTools<HealthTools>()
                    .WithTools<SourcesTools>()
                    .WithTools<SessionsTools>()
                    .WithTools<SearchProfilesTools>()
                    .WithTools<SearchProfilesWriteTools>()
                    .WithTools<RunsTools>()
                    .WithTools<RunActionsTools>();

                var host = builder.Build();

                DatabaseRegistration.MigrateInDevelopment(host);

                await host.RunAsync();
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }
    }
}
