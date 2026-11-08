using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Registers the SQLite persistence ports. Everything is scoped: one
    /// <see cref="JobScrapeDbContext"/> per run or request.
    /// </summary>
    public static class PersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the context, unit of work, repositories, and <see cref="ScrapeStores"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">A Microsoft.Data.Sqlite connection string.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the connection string is blank.</exception>
        public static IServiceCollection AddJobScrapePersistence(
            this IServiceCollection services,
            string connectionString
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            services.AddSingleton<SqlitePragmaInterceptor>();
            services.AddDbContext<JobScrapeDbContext>(
                (provider, options) =>
                    options
                        .UseSqlite(connectionString)
                        .AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>())
            );
            services.AddScoped<IUnitOfWork>(provider =>
                provider.GetRequiredService<JobScrapeDbContext>()
            );
            services.AddScoped<ISearchProfileRepository, SearchProfileRepository>();
            services.AddScoped<ISearchProfileStore, SearchProfileReadStore>();
            services.AddScoped<ISessionStore, SessionReadStore>();
            services.AddScoped<IRunStore, RunReadStore>();

            // Read stores evaluate time-dependent state; a host that already
            // registered its own clock (or a fake) keeps it.
            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<IScrapeRunRepository, ScrapeRunRepository>();
            services.AddScoped<IJobPostingRepository, JobPostingRepository>();
            services.AddScoped<IBrowserProfileRepository, BrowserProfileRepository>();
            services.AddScoped<ISourceQuotaRepository, SourceQuotaRepository>();
            services.AddScoped<IUsageQuotaRepository, UsageQuotaRepository>();
            services.AddScoped<ISourceCircuitBreakerRepository, SourceCircuitBreakerRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IIdempotencyStore, IdempotencyStore>();
            services.AddScoped(provider => new ScrapeStores(
                provider.GetRequiredService<ISearchProfileRepository>(),
                provider.GetRequiredService<IScrapeRunRepository>(),
                provider.GetRequiredService<IJobPostingRepository>(),
                provider.GetRequiredService<IBrowserProfileRepository>(),
                provider.GetRequiredService<ISourceQuotaRepository>(),
                provider.GetRequiredService<ISourceCircuitBreakerRepository>(),
                provider.GetRequiredService<IUnitOfWork>()
            ));
            return services;
        }
    }
}
