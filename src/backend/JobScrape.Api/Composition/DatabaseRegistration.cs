using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Api.Composition
{
    /// <summary>
    /// Wires SQLite persistence for the API. The database path comes from
    /// <c>JobScrape:Storage:DatabasePath</c>, resolved against the content
    /// root; its directory is created because SQLite does not create
    /// directories. Migrations are applied automatically only in
    /// Development; elsewhere they stay an explicit operator step
    /// (docs/02-architecture.md §10.5).
    /// </summary>
    public static class DatabaseRegistration
    {
        /// <summary>
        /// Registers the persistence ports against the configured database file.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The app configuration.</param>
        /// <param name="contentRoot">Root for a relative database path.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IServiceCollection AddJobScrapeDatabase(
            this IServiceCollection services,
            IConfiguration configuration,
            string contentRoot
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(contentRoot);

            // Read directly (not through IOptions) because registration needs
            // the path now; the same section is still validated on start.
            var storage =
                configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                ?? new StorageOptions();
            var path = Path.GetFullPath(storage.DatabasePath, contentRoot);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return services.AddJobScrapePersistence(
                new SqliteConnectionStringBuilder { DataSource = path }.ToString()
            );
        }

        /// <summary>
        /// Applies pending migrations when running in Development.
        /// </summary>
        /// <param name="app">The built application.</param>
        /// <returns>True when migrations were applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        public static bool MigrateInDevelopment(WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            if (!app.Environment.IsDevelopment())
            {
                return false;
            }

            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<JobScrapeDbContext>().Database.Migrate();
            return true;
        }
    }
}
