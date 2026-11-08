using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Features.Quotas;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Features.Sources;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Export;
using JobScrape.Infrastructure.Runtime;
using JobScrape.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            // The bearer token owns the identity: sub is the actor, role
            // claims are the roles. Unauthenticated callers get a denied
            // (empty-permission) context instead of the local operator, so a
            // missing auth gate can never silently run as a trusted actor.
            services.AddHttpContextAccessor();
            services.AddScoped<ICallerContext>(provider =>
            {
                var user = provider.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true)
                {
                    return new CallerContext(
                        "unauthenticated",
                        Array.Empty<string>(),
                        Array.Empty<string>()
                    );
                }

                var mustChange = user.HasClaim(claim =>
                    claim.Type == IdentityClaimTypes.MustChangePassword && claim.Value == "true"
                );
                var actor =
                    user.FindFirst(IdentityClaimTypes.Subject)?.Value
                    ?? user.FindFirst(IdentityClaimTypes.Email)?.Value
                    ?? CallerContext.LocalOperator.ActorId;
                var roles = user.FindAll(IdentityClaimTypes.Role)
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var permissions = user.FindAll(IdentityClaimTypes.Permission)
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new CallerContext(actor, roles, permissions)
                {
                    MustChangePassword = mustChange,
                };
            });

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
            // The export writers: resolve-time factories reading FeaturesOptions,
            // so a disabled format's writer is never constructed. Registered as
            // one explicit IEnumerable<IPostingExportWriter>, which replaces the
            // container's default enumeration for this service type; new export
            // writers are added inside this factory, not as separate registrations.
            services.AddSingleton<IEnumerable<IPostingExportWriter>>(provider =>
            {
                var features = provider.GetRequiredService<IOptions<FeaturesOptions>>().Value;
                var writers = new List<IPostingExportWriter>();
                if (features.CsvExport)
                {
                    writers.Add(new CsvPostingWriter());
                }

                if (features.ExcelExport)
                {
                    writers.Add(new ExcelPostingWriter());
                }

                return writers;
            });
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
            services.AddTransient<ListUsageQuotasHandler>();
            services.AddTransient<UpdateUsageQuotaHandler>();
            services.AddTransient<LoginHandler>();
            services.AddTransient<SetupPasswordHandler>();
            services.AddTransient<ChangePasswordHandler>();
            services.AddTransient<CreateUserHandler>(provider => new CreateUserHandler(
                provider.GetRequiredService<IUserRepository>(),
                provider.GetRequiredService<IRoleRepository>(),
                provider.GetRequiredService<IUnitOfWork>(),
                provider.GetRequiredService<TimeProvider>()
            ));
            services.AddTransient<GetUserHandler>();
            services.AddTransient<ListUsersHandler>();
            services.AddTransient<AssignRoleHandler>();
            services.AddTransient<RevokeRoleHandler>();
            services.AddTransient<CreateRoleHandler>();
            services.AddTransient<GetRoleHandler>();
            services.AddTransient<ListRolesHandler>();
            services.AddTransient<GrantPermissionHandler>();
            services.AddTransient<RevokePermissionHandler>();
            services.AddHostedService(provider =>
            {
                // Config-first admin seed: appsettings carries the local
                // development defaults so plain `dotnet run` seeds the first
                // admin with no extra CLI parameters. Hierarchical env vars
                // (JobScrape__AdminSeed__*) override the file via the
                // configuration provider. The legacy flat names (ADMIN_EMAIL /
                // ADMIN_SETUP_SECRET) apply only when the JobScrape:AdminSeed
                // section values are absent, e.g. when that section is
                // removed from the production configuration.
                var seed = provider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
                var email = seed.Email ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");
                var secret =
                    seed.SetupSecret ?? Environment.GetEnvironmentVariable("ADMIN_SETUP_SECRET");
                return new AdminSeedHostedService(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<AdminSeedHostedService>>(),
                    email,
                    secret
                );
            });
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
