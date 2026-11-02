namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Browser engine a profile launches. Values are never repurposed.
    /// </summary>
    public enum BrowserEngine
    {
        /// <summary>
        /// Chromium (the default; also hosts the Chrome and Edge channels).
        /// </summary>
        Chromium,

        /// <summary>
        /// Playwright's patched Firefox.
        /// </summary>
        Firefox,

        /// <summary>
        /// Playwright's WebKit build. Not real Safari.
        /// </summary>
        WebKit,
    }
}
