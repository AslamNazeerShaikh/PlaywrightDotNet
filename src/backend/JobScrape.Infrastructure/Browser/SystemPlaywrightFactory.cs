using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Creates the real Playwright driver.
    /// </summary>
    public sealed class SystemPlaywrightFactory : IPlaywrightFactory
    {
        /// <inheritdoc />
        public Task<IPlaywright> CreateAsync()
        {
            return Playwright.CreateAsync();
        }
    }
}
