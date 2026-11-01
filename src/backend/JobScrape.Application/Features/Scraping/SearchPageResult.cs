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
    public sealed record SearchPageResult(
        IReadOnlyList<JobSnapshot> Jobs,
        string? NextCursor,
        PageFingerprint? Fingerprint,
        ChallengeAssessment Challenge
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
    }
}
