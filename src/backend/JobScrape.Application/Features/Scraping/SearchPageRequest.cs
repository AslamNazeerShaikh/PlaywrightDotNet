namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Which result page to fetch.
    /// </summary>
    /// <param name="PageNumber">One-based page number within the run.</param>
    /// <param name="Cursor">
    /// The opaque continuation from the previous page's
    /// <see cref="SearchPageResult.NextCursor"/>; null for the first page.
    /// </param>
    public sealed record SearchPageRequest(int PageNumber, string? Cursor);
}
