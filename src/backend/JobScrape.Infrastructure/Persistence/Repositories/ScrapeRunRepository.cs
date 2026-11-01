using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IReadOnlyList<ScrapeRun>> ListExecutingAsync(
            CancellationToken cancellationToken
        )
        {
            RunStage[] executing =
            [
                RunStage.Pending,
                RunStage.StartingSession,
                RunStage.Searching,
                RunStage.Enriching,
                RunStage.Persisting,
            ];
            return await db
                .ScrapeRuns.Where(run => executing.Contains(run.Stage))
                .OrderBy(run => run.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
