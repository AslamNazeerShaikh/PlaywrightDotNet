using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Owns persistent browser sessions. Login is always manual; this port
    /// never automates credentials. Per-source warm-up lives in the
    /// source adapters, behind this seam.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Opens (or reuses) the profile's session for a source and checks it
        /// is usable.
        /// </summary>
        /// <param name="source">The source to verify against.</param>
        /// <param name="profileId">The browser profile to launch.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// <see cref="ChallengeAssessment.Clear"/> when the session is ready
        /// (or the source has no warm-up to run); otherwise the wall that
        /// blocks it (e.g. login required).
        /// </returns>
        Task<ChallengeAssessment> EnsureSessionAsync(
            SourceKey source,
            BrowserProfileId profileId,
            CancellationToken cancellationToken
        );
    }
}
