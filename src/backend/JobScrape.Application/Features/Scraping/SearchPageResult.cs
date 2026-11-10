using JobScrape.Domain.Access;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// One fetched result page as classified by the adapter. A wall is
    /// reported through <see cref="Challenge"/>, never thrown, and a page
    /// with a wall carries no jobs.
    /// </summary>
    /// <param name="Jobs">Snapshots of the visible, non-honeypot cards.</param>
    /// <param name="NextCursor">Continuation for the next page; null when there is none.</param>
    /// <param name="Fingerprint">Identity of this page, for repeat detection; null when blocked.</param>
    /// <param name="Challenge">The page classification.</param>
    /// <param name="LastPage">The listing's last page when the page reports it; null otherwise.</param>
    public sealed record SearchPageResult(
        IReadOnlyList<JobSnapshot> Jobs,
        string? NextCursor,
        PageFingerprint? Fingerprint,
        ChallengeAssessment Challenge,
        int? LastPage = null
    )
    {
        /// <summary>
        /// Creates a blocked page carrying only its classification.
        /// </summary>
        /// <param name="challenge">The wall or failure.</param>
        /// <returns>A result with no jobs and no continuation.</returns>
        public static SearchPageResult Blocked(ChallengeAssessment challenge)
        {
            return new SearchPageResult([], null, null, challenge);
        }

        /// <summary>
        /// Creates a refused page: the adapter was asked for a page beyond
        /// the listing and reports the refusal instead of content. The
        /// result carries no jobs, no continuation, no fingerprint, and a
        /// clear challenge; the last page is the requested page minus one.
        /// </summary>
        /// <param name="requestedPage">The page number the adapter was asked for; must be at least 2.</param>
        /// <returns>An empty result naming the listing's last page.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="requestedPage"/> is below 2.</exception>
        public static SearchPageResult BeyondLastPage(int requestedPage)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(requestedPage, 2);
            return new SearchPageResult(
                [],
                null,
                null,
                ChallengeAssessment.Clear,
                requestedPage - 1
            );
        }
    }
}
