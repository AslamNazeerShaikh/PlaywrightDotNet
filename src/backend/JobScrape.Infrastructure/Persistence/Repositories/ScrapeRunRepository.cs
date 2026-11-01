using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IScrapeRunRepository"/>.
    /// </summary>
    internal sealed class ScrapeRunRepository(JobScrapeDbContext db) : IScrapeRunRepository
    {
        public async Task<ScrapeRun?> GetAsync(ScrapeRunId id, CancellationToken cancellationToken)
        {
            return await db.ScrapeRuns.FindAsync([id], cancellationToken);
        }

        public async Task AddAsync(ScrapeRun run, CancellationToken cancellationToken)
        {
            await db.ScrapeRuns.AddAsync(run, cancellationToken);
        }
    }
}
