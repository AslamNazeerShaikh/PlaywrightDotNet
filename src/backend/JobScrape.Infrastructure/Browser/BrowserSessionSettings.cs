namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Resolved browser settings: absolute directories plus launch options.
    /// </summary>
    /// <param name="ProfilesRoot">Absolute root of the per-profile user-data directories.</param>
    /// <param name="ArtifactsRoot">Absolute root for run artifacts.</param>
    /// <param name="Headless">Whether browsers launch headless.</param>
    /// <param name="NavigationTimeout">How long a navigation may take.</param>
    public sealed record BrowserSessionSettings(
        string ProfilesRoot,
        string ArtifactsRoot,
        bool Headless,
        TimeSpan NavigationTimeout
    );
}
