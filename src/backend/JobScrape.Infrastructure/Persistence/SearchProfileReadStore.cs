using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core <see cref="ISearchProfileStore"/>: no-tracking reads mapped to
    /// the read model. Criteria and limits are JSON columns, so mapping
    /// happens after materialization rather than in SQL.
    /// </summary>
    public sealed class SearchProfileReadStore : ISearchProfileStore
    {
        // Injected and never mutated; scoped with the unit of work.
        private readonly JobScrapeDbContext _db;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchProfileReadStore"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is null.</exception>
        public SearchProfileReadStore(JobScrapeDbContext db)
        {
            ArgumentNullException.ThrowIfNull(db);
            _db = db;
        }

        /// <summary>
        /// Returns the profile with the given identifier, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null.</returns>
        public async Task<SearchProfileResponse?> GetAsync(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            var key = SearchProfileId.From(id);
            var profile = await _db
                .SearchProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
            return profile is null ? null : SearchProfileResponse.From(profile);
        }

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profiles.</returns>
        public async Task<IReadOnlyList<SearchProfileResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            var profiles = await _db
                .SearchProfiles.AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
            return profiles.Select(SearchProfileResponse.From).ToArray();
        }
    }
}
