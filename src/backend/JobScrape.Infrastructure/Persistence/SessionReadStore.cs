using JobScrape.Application.Features.Sessions;
using JobScrape.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core <see cref="ISessionStore"/>: no-tracking reads of browser
    /// profiles mapped to <see cref="SessionResponse"/>, with launchability
    /// evaluated against the injected clock.
    /// </summary>
    public sealed class SessionReadStore : ISessionStore
    {
        // Both injected and never mutated; scoped with the unit of work.
        private readonly JobScrapeDbContext _db;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionReadStore"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clock">The time source for <c>CanLaunch</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public SessionReadStore(JobScrapeDbContext db, TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(clock);
            _db = db;
            _clock = clock;
        }

        /// <summary>
        /// Returns the profile with the given identifier, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null.</returns>
        public async Task<SessionResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            var key = BrowserProfileId.From(id);
            var profile = await _db
                .BrowserProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
            return profile is null ? null : SessionResponse.From(profile, _clock.GetUtcNow());
        }

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profiles.</returns>
        public async Task<IReadOnlyList<SessionResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            var now = _clock.GetUtcNow();
            var profiles = await _db
                .BrowserProfiles.AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
            return profiles.Select(p => SessionResponse.From(p, now)).ToArray();
        }
    }
}
