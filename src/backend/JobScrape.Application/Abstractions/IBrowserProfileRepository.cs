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
    }
}
