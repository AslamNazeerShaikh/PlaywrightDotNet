using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IScrapeRunProgressRepository"/>.
    /// </summary>
    internal sealed class ScrapeRunProgressRepository(JobScrapeDbContext db)
        : IScrapeRunProgressRepository
    {
        public async Task<ScrapeRunProgress?> GetAsync(
            ScrapeRunId id,
            CancellationToken cancellationToken
        )
        {
            return await db.ScrapeRunProgress.FindAsync([id], cancellationToken);
        }

        public async Task SaveAsync(ScrapeRunProgress progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            var existing = await db.ScrapeRunProgress.FindAsync(
                [progress.Id],
                cancellationToken
            );
            if (existing is null)
            {
                await db.ScrapeRunProgress.AddAsync(progress, cancellationToken);
            }
            else
            {
                existing.RecordPage(
                    progress.PagesCompleted,
                    progress.NextCursor,
                    progress.SeenExternalIds,
                    progress.UpdatedAt
                );
            }
        }
    }
}
