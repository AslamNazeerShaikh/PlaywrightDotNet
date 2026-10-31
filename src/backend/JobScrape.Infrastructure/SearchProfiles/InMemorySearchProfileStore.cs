using System.Collections.Concurrent;
using JobScrape.Application.Features.SearchProfiles;

namespace JobScrape.Infrastructure.SearchProfiles
{
    /// <summary>
    /// In-memory <see cref="ISearchProfileStore"/> for the walking skeleton.
    /// No persistence yet: Phase 3 replaces this with the EF Core SQLite
    /// repository behind the same port, without touching the handler.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    public sealed class InMemorySearchProfileStore : ISearchProfileStore
    {
        // The only mutable state; concurrent readers/writers are safe.
        private readonly ConcurrentDictionary<Guid, SearchProfileResponse> _profiles = new();

        /// <summary>
        /// Adds or replaces a profile. Used for seeding and tests; the
        /// production repository will upsert instead.
        /// </summary>
        /// <param name="profile">The profile summary to store. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        public void Add(SearchProfileResponse profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            _profiles[profile.Id] = profile;
        }

        /// <summary>
        /// Returns the profile with the given identifier, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile summary, or null when no profile has that id.</returns>
        public Task<SearchProfileResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            // Synchronous dictionary read wrapped for the async port contract;
            // no I/O here, so no await and no cancellation check needed.
            _profiles.TryGetValue(id, out var profile);
            return Task.FromResult(profile);
        }
    }
}
