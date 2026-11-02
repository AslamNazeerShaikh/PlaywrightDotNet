using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="SearchProfile"/> aggregates. Name
    /// uniqueness is enforced by the store and surfaces as a conflict.
    /// </summary>
    public interface ISearchProfileRepository
    {
        /// <summary>
        /// Loads a profile, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null.</returns>
        Task<SearchProfile?> GetAsync(SearchProfileId id, CancellationToken cancellationToken);

        /// <summary>
        /// Whether a profile with exactly this name exists. The store's unique
        /// index remains the final guard against a concurrent insert.
        /// </summary>
        /// <param name="name">The trimmed profile name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>True when the name is taken.</returns>
        Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new profile; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the profile is staged.</returns>
        Task AddAsync(SearchProfile profile, CancellationToken cancellationToken);
    }
}
