using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Features.Sources;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Export;
using JobScrape.Infrastructure.Runtime;
using JobScrape.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Composition
{
    /// <summary>
    /// Registers the explicit use-case handlers and the background run
    /// wiring both hosts share. Every handler is listed one by one: no
    /// assembly scanning, no mediator, so "Find usages" on a handler finds
    /// its callers.
    /// </summary>
    public static class UseCaseRegistration
    {
        /// <summary>
        /// Registers the clock, the caller, every handler, the export
        /// writers, and the run coordinator.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddJobScrapeUseCases(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // System clock as the ambient time source; handlers take
            // TimeProvider so tests can substitute a fake.
            services.AddSingleton(TimeProvider.System);

            // Loopback only and no auth in this repo, so every MCP call
            // runs as the local operator.
            services.AddScoped<ICallerContext>(_ => CallerContext.LocalOperator);

            services.AddTransient<GetHealthHandler>();
            services.AddTransient<GetSearchProfileHandler>();
            services.AddTransient<ListSearchProfilesHandler>();
            services.AddTransient<CreateSearchProfileHandler>();
            services.AddTransient<UpdateSearchProfileHandler>();
            services.AddTransient<ListSourcesHandler>();
            services.AddTransient<ListSessionsHandler>();
            services.AddTransient<GetSessionHandler>();
            services.AddTransient<CreateSessionHandler>();
            services.AddTransient<UpdateSessionHandler>();
            services.AddTransient<StartRunHandler>();
            services.AddTransient<ListRunsHandler>();
            services.AddTransient<GetRunHandler>();
            services.AddTransient<CancelRunHandler>();
            services.AddTransient<ListRunPostingsHandler>();
            services.AddTransient<ExportRunPostingsHandler>();
            services.AddSingleton<IPostingExportWriter, CsvPostingWriter>();
            services.AddSingleton<IPostingExportWriter, ExcelPostingWriter>();
            // Identity: the token service is stateless, so it shares the
            // validated options snapshot; the policy record keeps JwtOptions
            // out of the application layer.
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton(provider =>
            {
                var jwt = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
                return new LoginPolicy(
                    jwt.MaxFailedAttempts,
                    TimeSpan.FromMinutes(jwt.LockoutMinutes)
                );
            });
            services.AddTransient<LoginHandler>();
            services.AddTransient<SetupPasswordHandler>();
            services.AddTransient<CreateUserHandler>();
            services.AddTransient<AssignRoleHandler>();
            services.AddTransient<RevokeRoleHandler>();
            services.AddTransient<CreateRoleHandler>();
            services.AddTransient<GrantPermissionHandler>();
            services.AddTransient<RevokePermissionHandler>();
            services.AddHostedService<AdminSeedHostedService>();
            // The scrape itself: scoped, because it holds the run's unit of
            // work; the coordinator resolves it in a fresh scope per run.
            services.AddScoped<RunScrapeHandler>();

            // Runs outlive their request: one coordinator executes them in
            // the background, cancels on shutdown, and fails runs a crash
            // left mid-flight on the next start.
            services.AddSingleton<RunCoordinator>();
            services.AddSingleton<IRunLauncher>(provider =>
                provider.GetRequiredService<RunCoordinator>()
            );
            services.AddHostedService(provider => provider.GetRequiredService<RunCoordinator>());
            return services;
        }
    }
}
