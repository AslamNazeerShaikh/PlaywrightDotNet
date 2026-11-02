using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Points an adapter at one posting's detail page.
    /// </summary>
    /// <param name="ExternalJobId">The source's job id.</param>
    /// <param name="Url">The canonical job URL.</param>
    public sealed record JobReference(ExternalJobId ExternalJobId, CanonicalUrl Url);
}
