using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="ISearchProfileRepository"/>.
    /// </summary>
    internal sealed class SearchProfileRepository(JobScrapeDbContext db) : ISearchProfileRepository
    {
        public async Task<SearchProfile?> GetAsync(
            SearchProfileId id,
            CancellationToken cancellationToken
        )
        {
            return await db.SearchProfiles.FindAsync([id], cancellationToken);
        }

        public async Task AddAsync(SearchProfile profile, CancellationToken cancellationToken)
        {
            await db.SearchProfiles.AddAsync(profile, cancellationToken);
        }
    }
}
