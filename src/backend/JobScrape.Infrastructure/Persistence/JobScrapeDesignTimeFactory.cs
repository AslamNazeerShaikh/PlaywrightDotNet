using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Builds the context for <c>dotnet ef</c> tooling only (migrations).
    /// Runtime code registers the context through
    /// <see cref="PersistenceServiceCollectionExtensions.AddJobScrapePersistence"/>.
    /// </summary>
    public sealed class JobScrapeDesignTimeFactory : IDesignTimeDbContextFactory<JobScrapeDbContext>
    {
        /// <summary>
        /// Creates a context bound to a throwaway design-time database.
        /// </summary>
        /// <param name="args">Tool arguments (unused).</param>
        /// <returns>The context.</returns>
        public JobScrapeDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<JobScrapeDbContext>()
                .UseSqlite("Data Source=design-time.db")
                .Options;
            return new JobScrapeDbContext(options);
        }
    }
}
