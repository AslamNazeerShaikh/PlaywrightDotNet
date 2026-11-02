namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// What a source adapter can do.
    /// </summary>
    /// <param name="SupportsDetails">True when <see cref="IJobSource.GetDetailAsync"/> can enrich postings.</param>
    public sealed record SourceCapabilities(bool SupportsDetails);
}
