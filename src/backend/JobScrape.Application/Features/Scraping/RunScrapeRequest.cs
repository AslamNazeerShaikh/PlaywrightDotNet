using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// A request to execute one search profile.
    /// </summary>
    /// <param name="ProfileId">The search profile to run.</param>
    /// <param name="BrowserProfileId">The browser profile whose session to use; must be bound to the same source.</param>
    /// <param name="Consent">The operator's risk acknowledgments.</param>
    /// <param name="Enrich">False skips detail pages (CLI <c>--no-enrich</c>).</param>
    public sealed record RunScrapeRequest(
        SearchProfileId ProfileId,
        BrowserProfileId BrowserProfileId,
        ComplianceConsent Consent,
        bool Enrich = true
    );
}
