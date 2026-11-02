using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="BrowserProfile"/> aggregates.
    /// </summary>
    public interface IBrowserProfileRepository
    {
        /// <summary>
        /// Loads a browser profile, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null.</returns>
        Task<BrowserProfile?> GetAsync(BrowserProfileId id, CancellationToken cancellationToken);

        /// <summary>
        /// Whether a browser profile with exactly this name exists, staged or
        /// saved. The store's unique index remains the final guard.
        /// </summary>
        /// <param name="name">The trimmed profile name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>True when the name is taken.</returns>
        Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new browser profile; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the profile is staged.</returns>
        Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken);
    }
}
