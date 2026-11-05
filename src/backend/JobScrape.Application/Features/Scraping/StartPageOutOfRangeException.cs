namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// The requested start page lies past the listing's last page, so the run
    /// fails gracefully instead of recording generic page 1 results as the window.
    /// </summary>
    /// <param name="StartPage">The requested start page.</param>
    /// <param name="MaxPage">The listing's last page for the observed page size.</param>
    public sealed class StartPageOutOfRangeException(int StartPage, int MaxPage)
        : Exception($"Start page {StartPage} is past the last page {MaxPage}.")
    {
        /// <summary>
        /// Gets the requested start page.
        /// </summary>
        public int StartPage { get; } = StartPage;

        /// <summary>
        /// Gets the listing's last page for the observed page size.
        /// </summary>
        public int MaxPage { get; } = MaxPage;
    }
}
