using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Read-model view of a scrape run: where it is in its lifecycle, what it
    /// found, and the caps it ran under. Timestamps are UTC.
    /// </summary>
    /// <param name="Id">The run identifier.</param>
    /// <param name="ProfileId">The search profile the run executed.</param>
    /// <param name="ProfileName">The profile's name, or null when the profile is gone.</param>
    /// <param name="SourceKey">The source, e.g. <c>naukri</c>.</param>
    /// <param name="Stage">Lifecycle stage, e.g. <c>Searching</c>, <c>Completed</c>.</param>
    /// <param name="IsTerminal">Whether the run has ended and accepts no transitions.</param>
    /// <param name="PagesFetched">Search pages fetched.</param>
    /// <param name="PostingsFound">Postings seen on those pages.</param>
    /// <param name="PostingsNew">Postings stored for the first time.</param>
    /// <param name="PostingsUpdated">Known postings whose content changed.</param>
    /// <param name="DetailsFetched">Detail pages fetched for enrichment.</param>
    /// <param name="Challenges">Walls hit.</param>
    /// <param name="Failures">Pages or details that failed after retries.</param>
    /// <param name="MaxPages">The run's page cap.</param>
    /// <param name="MaxJobs">The run's posting cap.</param>
    /// <param name="MaxDetails">The run's detail cap.</param>
    /// <param name="PauseReason">The wall that paused the run, while paused.</param>
    /// <param name="RetryAt">When a rate-limited run may resume.</param>
    /// <param name="FailureReason">Why the run failed, once failed.</param>
    /// <param name="CreatedAt">When the run was created.</param>
    /// <param name="StartedAt">When the run started.</param>
    /// <param name="FinishedAt">When the run reached a terminal stage.</param>
    public sealed record RunResponse(
        Guid Id,
        Guid ProfileId,
        string? ProfileName,
        string SourceKey,
        string Stage,
        bool IsTerminal,
        int PagesFetched,
        int PostingsFound,
        int PostingsNew,
        int PostingsUpdated,
        int DetailsFetched,
        int Challenges,
        int Failures,
        int MaxPages,
        int MaxJobs,
        int MaxDetails,
        string? PauseReason,
        DateTimeOffset? RetryAt,
        string? FailureReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? FinishedAt
    )
    {
        /// <summary>
        /// Maps a run to its read model.
        /// </summary>
        /// <param name="run">The run. Must not be null.</param>
        /// <param name="profileName">The profile's name, when it still exists.</param>
        /// <returns>The read model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="run"/> is null.</exception>
        public static RunResponse From(ScrapeRun run, string? profileName)
        {
            ArgumentNullException.ThrowIfNull(run);
            return new RunResponse(
                run.Id.Value,
                run.ProfileId.Value,
                profileName,
                run.Source.Value,
                run.Stage.ToString(),
                run.IsTerminal,
                run.Stats.PagesFetched,
                run.Stats.PostingsFound,
                run.Stats.PostingsNew,
                run.Stats.PostingsUpdated,
                run.Stats.DetailsFetched,
                run.Stats.Challenges,
                run.Stats.Failures,
                run.Limits.MaxPages,
                run.Limits.MaxJobs,
                run.Limits.MaxDetails,
                run.PauseReason?.ToString(),
                run.RetryAt,
                run.FailureReason,
                run.CreatedAt,
                run.StartedAt,
                run.FinishedAt
            );
        }
    }
}
