using JobScrape.Domain.Access;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// The per-source half of browser warm-up (docs/08-site-playbooks.md
    /// §3.2): <see cref="PlaywrightSessionManager"/> opens the profile's
    /// page, the source's adapter verifies it the way a person would.
    /// Per-source quirks live with their adapter instead of behind a
    /// source check in the session manager.
    /// </summary>
    public interface ISourceWarmUpVerifier
    {
        /// <summary>
        /// Warms up the profile's page and reports the outcome. Never types
        /// credentials: login stays manual.
        /// </summary>
        /// <param name="page">The page the session manager opened. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// The wall that blocks the session, or <see cref="ChallengeAssessment.Clear"/>
        /// when it is ready; null when the source has no warm-up to run, in
        /// which case the session is reported ready without marking the
        /// profile verified.
        /// </returns>
        Task<ChallengeAssessment?> VerifyAsync(IPage page, CancellationToken cancellationToken);
    }
}
