using JobScrape.Application.Abstractions;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// The persistence ports a scrape run writes through, grouped so the
    /// handler's constructor stays readable. Every member is still an
    /// explicitly registered port; this is not a service locator.
    /// </summary>
    /// <param name="Profiles">Search profiles.</param>
    /// <param name="Runs">Scrape runs.</param>
    /// <param name="Postings">Job postings.</param>
    /// <param name="BrowserProfiles">Browser profiles.</param>
    /// <param name="Quotas">Per-source daily quotas.</param>
    /// <param name="Circuits">Per-source circuit breakers.</param>
    /// <param name="Progress">Per-run scrape positions for resume.</param>
    /// <param name="UnitOfWork">Commits all staged changes.</param>
    public sealed record ScrapeStores(
        ISearchProfileRepository Profiles,
        IScrapeRunRepository Runs,
        IJobPostingRepository Postings,
        IBrowserProfileRepository BrowserProfiles,
        ISourceQuotaRepository Quotas,
        ISourceCircuitBreakerRepository Circuits,
        IScrapeRunProgressRepository Progress,
        IUnitOfWork UnitOfWork
    );
}
