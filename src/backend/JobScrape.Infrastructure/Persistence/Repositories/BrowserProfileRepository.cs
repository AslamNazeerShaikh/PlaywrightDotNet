using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IBrowserProfileRepository"/>.
    /// </summary>
    internal sealed class BrowserProfileRepository(JobScrapeDbContext db)
        : IBrowserProfileRepository
    {
        public async Task<BrowserProfile?> GetAsync(
            BrowserProfileId id,
            CancellationToken cancellationToken
        )
        {
            return await db.BrowserProfiles.FindAsync([id], cancellationToken);
        }
    }
}
