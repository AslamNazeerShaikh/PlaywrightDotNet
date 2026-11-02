using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Everything an adapter needs to know about the run it serves.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Source">The source being searched.</param>
    /// <param name="BrowserProfileId">The browser session to use.</param>
    /// <param name="Criteria">The search criteria.</param>
    public sealed record SourceRunContext(
        ScrapeRunId RunId,
        SourceKey Source,
        BrowserProfileId BrowserProfileId,
        SearchCriteria Criteria
    );
}
