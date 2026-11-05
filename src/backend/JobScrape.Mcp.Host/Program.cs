using JobScrape.Infrastructure.Composition;
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
            try
            {
                var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

                // Stdout is the JSON-RPC channel: the console sink writes
                // to stderr so framing on stdout stays intact.
                builder.Services.AddJobScrapeLogging(
                    builder.Configuration,
                    builder.Environment.ContentRootPath,
                    consoleToStandardError: true,
                    applicationName: "JobScrape.Mcp.Host",
                    logFilePrefix: "jobscrape-mcp-host"
                );

                // One list, shared with the API host: clock, caller,
                // handlers, and the background run coordinator.
                builder.Services.AddJobScrapeUseCases();

                // Bound and validated once; bad caps or consent flags fail
                // startup instead of running unsafely.
                builder.Services.AddJobScrapeOptions();

                // Playwright, the session manager, wall artifacts, and adapters.
                builder.Services.AddJobScrapeBrowser(builder.Environment.ContentRootPath);

                // SQLite through EF Core: one scoped unit of work per request.
                builder.Services.AddJobScrapeDatabase(
                    builder.Configuration,
                    builder.Environment.ContentRootPath
                );

                builder.Services.AddMcpServer().WithStdioServerTransport().WithTools(McpTools.All);

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
