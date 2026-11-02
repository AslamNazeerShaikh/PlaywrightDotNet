using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.EntityFrameworkCore;

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

        public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken)
        {
            return db.BrowserProfiles.Local.Any(profile => profile.Name == name)
                || await db.BrowserProfiles.AnyAsync(
                    profile => profile.Name == name,
                    cancellationToken
                );
        }

        public async Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken)
        {
            await db.BrowserProfiles.AddAsync(profile, cancellationToken);
        }
    }
}
