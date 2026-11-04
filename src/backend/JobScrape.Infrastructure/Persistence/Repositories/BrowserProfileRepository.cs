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
            return db.BrowserProfiles.Local.Any(profile =>
                    string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)
                )
                || await db.BrowserProfiles.AnyAsync(
                    profile => EF.Functions.Collate(profile.Name, "NOCASE") == name,
                    cancellationToken
                );
        }

        public async Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken)
        {
            await db.BrowserProfiles.AddAsync(profile, cancellationToken);
        }
    }
}
