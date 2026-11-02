namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Which build of the engine to launch. Branded channels exist only for
    /// <see cref="BrowserEngine.Chromium"/>. Values are never repurposed.
    /// </summary>
    public enum BrowserChannel
    {
        /// <summary>
        /// The Playwright-bundled, version-pinned build (default).
        /// </summary>
        Bundled,

        /// <summary>
        /// The installed Google Chrome stable channel.
        /// </summary>
        Chrome,

        /// <summary>
        /// The installed Microsoft Edge stable channel.
        /// </summary>
        Edge,
    }
}
