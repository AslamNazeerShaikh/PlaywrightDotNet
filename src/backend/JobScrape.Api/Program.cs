using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Infrastructure.Composition;
using JobScrape.Mcp.Tools;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
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
            // Local, gitignored overrides (for example uBOL enablement).
            if (builder.Environment.IsDevelopment())
            {
                builder.Configuration.AddJsonFile(
                    "appsettings.Local.json",
                    optional: true,
                    reloadOnChange: false
                );
            }

            // Controllers first: the API surface is controller-based so every
            // endpoint stays greppable and block-namespaced (no minimal-API
            // lambdas hidden in this method).
            builder.Services.AddControllers();
            // Validation failures map to coded problems inside the actions
            // (see the auth controllers): the framework's automatic 400 is
            // off so every 400 carries the stable code and trace id.
            builder.Services.Configure<ApiBehaviorOptions>(options =>
                options.SuppressModelStateInvalidFilter = true
            );

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

            // One list for the single host: clock, caller, handlers,
            // export writers, and the background run coordinator.
            builder.Services.AddMemoryCache();
            builder.Services.AddJobScrapeUseCases();

            // Bound and validated once; bad caps or consent flags fail
            // startup instead of running unsafely.
            builder.Services.AddJobScrapeOptions();

            // HS256 bearer tokens, the admin and per-permission policies
            // with a fallback requiring an authenticated user, and the
            // fixed-window rate limiters.
            builder.Services.AddJobScrapeAuth(builder.Configuration);
            builder.Services.AddJobScrapeRateLimiting();

            // Serilog owns sinks and enrichers; app code logs through ILogger<T>.
            builder.Services.AddJobScrapeLogging(
                builder.Configuration,
                builder.Environment.ContentRootPath
            );

            // Playwright, the session manager, wall artifacts, and adapters.
            builder.Services.AddJobScrapeBrowser(builder.Environment.ContentRootPath);

            // SQLite through EF Core: one scoped unit of work per request.
            builder.Services.AddJobScrapeDatabase(
                builder.Configuration,
                builder.Environment.ContentRootPath
            );

            // Raw SQLite explorer for the cockpit database page. Uses the same
            // database file as EF Core; table/column names are validated.
            builder.Services.AddJobScrapeDatabaseExplorer(
                builder.Configuration,
                builder.Environment.ContentRootPath
            );

            // MCP over Streamable HTTP. Stateless: no session affinity, no
            // SSE endpoints, one server context per request. Tools come
            // from the shared McpTools.All list: a fixed type list, no
            // assembly scanning.
            builder
                .Services.AddMcpServer()
                .WithHttpTransport(McpHttpTransport.ConfigureStateless)
                .WithTools(McpTools.All);

            var app = builder.Build();

            // Development-only document and reference endpoints; never
            // proxied by the cockpit BFF and never exposed beyond loopback.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi().AllowAnonymous();

                app.MapScalarApiReference(
                        "/scalar",
                        options =>
                            options
                                .WithTitle("JobLens API v1")
                                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                                .AddPreferredSecuritySchemes("Bearer")
                    )
                    .AllowAnonymous();
            }

            DatabaseRegistration.MigrateInDevelopment(app);

            app.UseMiddleware<CorrelationMiddleware>();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseExceptionHandler();
            // One completion event per request, written through this host's
            // logger rather than the static Log.Logger.
            app.UseSerilogRequestLogging(options =>
                options.Logger = app.Services.GetRequiredService<Serilog.ILogger>()
            );
            app.UseMiddleware<UsageQuotaMiddleware>();
            app.UseMiddleware<IdempotencyMiddleware>();
            app.MapControllers();

            // MCP lives beside the controllers on /mcp; loopback only, like
            // every other endpoint in this host, and bearer-authorized.
            app.MapMcp("/mcp").RequireAuthorization();

            // Run disposes the host on every exit path; that disposes the
            // container-owned Serilog logger and flushes the async file sink
            // (docs/15-runtime-hardening.md §2.9).
            app.Run();
        }
    }
}
