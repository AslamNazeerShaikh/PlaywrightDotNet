using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Creates the Playwright driver. A port so the session pool is
    /// testable without launching a browser.
    /// </summary>
    public interface IPlaywrightFactory
    {
        /// <summary>
        /// Creates the Playwright driver.
        /// </summary>
        /// <returns>The driver.</returns>
        Task<IPlaywright> CreateAsync();
    }
}
