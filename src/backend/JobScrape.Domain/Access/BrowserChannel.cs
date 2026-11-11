namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Which build of the engine to launch. Only the Playwright-bundled,
    /// version-pinned build is supported. Values are never repurposed.
    /// </summary>
    public enum BrowserChannel
    {
        /// <summary>
        /// The Playwright-bundled, version-pinned build.
        /// </summary>
        Bundled,
    }
}
