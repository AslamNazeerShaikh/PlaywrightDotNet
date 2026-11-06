using System.Text;
using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Identity;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// The SQLite unit of work: one instance per run or request, never
    /// long-lived (docs/15-runtime-hardening.md §6.7). Tables and columns
    /// are snake_case; timestamps are stored as UTC ticks.
    /// </summary>
    public sealed class JobScrapeDbContext : DbContext, IUnitOfWork
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobScrapeDbContext"/> class.
        /// </summary>
        /// <param name="options">The configured options.</param>
        public JobScrapeDbContext(DbContextOptions<JobScrapeDbContext> options)
            : base(options) { }

        /// <summary>
        /// Gets the search profiles.
        /// </summary>
        public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();

        /// <summary>
        /// Gets the scrape runs.
        /// </summary>
        public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();

        /// <summary>
        /// Gets the job postings.
        /// </summary>
        public DbSet<JobPosting> JobPostings => Set<JobPosting>();

        /// <summary>
        /// Gets the browser profiles.
        /// </summary>
        public DbSet<BrowserProfile> BrowserProfiles => Set<BrowserProfile>();

        /// <summary>
        /// Gets the daily source quotas.
        /// </summary>
        public DbSet<SourceQuota> SourceQuotas => Set<SourceQuota>();

        /// <summary>
        /// Gets the source circuit breakers.
        /// </summary>
        public DbSet<SourceCircuitBreaker> SourceCircuitBreakers => Set<SourceCircuitBreaker>();

        /// <summary>
        /// Gets the login accounts.
        /// </summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>
        /// Gets the named roles.
        /// </summary>
        public DbSet<Role> Roles => Set<Role>();

        /// <summary>
        /// Gets the stored idempotency responses.
        /// </summary>
        internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

        /// <summary>
        /// Stores every <see cref="DateTimeOffset"/> as UTC ticks.
        /// </summary>
        /// <param name="configurationBuilder">The convention builder.</param>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            ArgumentNullException.ThrowIfNull(configurationBuilder);
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        }

        /// <summary>
        /// Applies each aggregate's configuration explicitly, then snake-cases
        /// every column that has no explicit name.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyConfiguration(new SearchProfileConfiguration());
            modelBuilder.ApplyConfiguration(new ScrapeRunConfiguration());
            modelBuilder.ApplyConfiguration(new JobPostingConfiguration());
            modelBuilder.ApplyConfiguration(new BrowserProfileConfiguration());
            modelBuilder.ApplyConfiguration(new SourceQuotaConfiguration());
            modelBuilder.ApplyConfiguration(new SourceCircuitBreakerConfiguration());
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());

            foreach (
                var property in modelBuilder
                    .Model.GetEntityTypes()
                    .SelectMany(e => e.GetProperties())
            )
            {
                if (property.GetColumnName() == property.Name)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }

        /// <summary>
        /// Converts <c>PascalCase</c> or <c>_camelCase</c> to <c>snake_case</c>.
        /// </summary>
        /// <param name="name">The property or field name.</param>
        /// <returns>The column name.</returns>
        private static string ToSnakeCase(string name)
        {
            var trimmed = name.TrimStart('_');
            var builder = new StringBuilder(trimmed.Length + 8);
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                if (char.IsUpper(c) && i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
    }
}
