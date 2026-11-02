using JobScrape.Domain.Access;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// One fetched detail page. Missing fields stay null, never faked.
    /// </summary>
    /// <param name="Detail">The enriched snapshot; null when blocked or unparsable.</param>
    /// <param name="Challenge">The page classification.</param>
    public sealed record JobDetailResult(JobSnapshot? Detail, ChallengeAssessment Challenge);
}
