using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Owns one persistent browser context per browser profile: the
    /// profile's user-data directory keeps its cookies between runs. A run
    /// opens its profile's context, adapters drive its page, and the run's
    /// host releases it when the run ends. Thread-safe.
    /// </summary>
    public interface IBrowserSessionPool
    {
        /// <summary>
        /// Gets the profile's page, launching its persistent context first
        /// when needed.
        /// </summary>
        /// <param name="profile">The browser profile. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the pool.</param>
        /// <returns>The profile's page.</returns>
        Task<IPage> OpenAsync(BrowserProfile profile, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the open page of a profile, if its context is open.
        /// </summary>
        /// <param name="profileId">The browser profile id.</param>
        /// <returns>The page, or null when the profile has no open context.</returns>
        IPage? Find(BrowserProfileId profileId);

        /// <summary>
        /// Closes a profile's context, flushing its cookies to disk. A no-op
        /// when the profile has none open.
        /// </summary>
        /// <param name="profileId">The browser profile id.</param>
        /// <returns>A task that completes when the context is closed.</returns>
        Task ReleaseAsync(BrowserProfileId profileId);
    }
}
