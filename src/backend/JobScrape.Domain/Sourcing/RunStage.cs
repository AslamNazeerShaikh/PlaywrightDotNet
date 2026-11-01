namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Lifecycle stage of a <see cref="ScrapeRun"/>
    /// (docs/03-domain-model.md §6.1). Values are never repurposed.
    /// </summary>
    public enum RunStage
    {
        /// <summary>
        /// Created; not yet started.
        /// </summary>
        Pending,

        /// <summary>
        /// Acquiring and verifying the browser session.
        /// </summary>
        StartingSession,

        /// <summary>
        /// Fetching result pages.
        /// </summary>
        Searching,

        /// <summary>
        /// Visiting detail pages.
        /// </summary>
        Enriching,

        /// <summary>
        /// Saving collected postings.
        /// </summary>
        Persisting,

        /// <summary>
        /// Stopped on a wall; waiting for a human to solve it and resume.
        /// </summary>
        PausedForChallenge,

        /// <summary>
        /// Throttled by the source; waiting for the retry window.
        /// </summary>
        RateLimited,

        /// <summary>
        /// Terminal: finished successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Terminal: stopped by an unrecoverable failure.
        /// </summary>
        Failed,

        /// <summary>
        /// Terminal: stopped by the operator; partial results are kept.
        /// </summary>
        Cancelled,
    }
}
