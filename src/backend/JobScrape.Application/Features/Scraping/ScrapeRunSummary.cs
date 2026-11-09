using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Outcome of a run that started. The stage tells the edge what happened
    /// (the CLI maps paused to exit code 3, rate limited to 4, failed to 6).
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Source">The source key.</param>
    /// <param name="Stage">The stage the run ended in.</param>
    /// <param name="Stats">The run counters.</param>
    /// <param name="PauseReason">The wall that paused the run, if paused.</param>
    /// <param name="RetryAt">When a rate-limited run may resume, if rate limited.</param>
    /// <param name="FailureReason">Why the run failed, if failed.</param>
    /// <param name="ArtifactDirectory">Captured evidence, relative to the artifacts root, if any.</param>
    /// <param name="OnlyNew">True when the run fetched only new postings.</param>
    public sealed record ScrapeRunSummary(
        Guid RunId,
        string Source,
        RunStage Stage,
        RunStats Stats,
        string? PauseReason,
        DateTimeOffset? RetryAt,
        string? FailureReason,
        string? ArtifactDirectory,
        bool OnlyNew
    )
    {
        /// <summary>
        /// Summarizes a run.
        /// </summary>
        /// <param name="run">The run. Must not be null.</param>
        /// <param name="artifactDirectory">Captured evidence, if any.</param>
        /// <returns>The summary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="run"/> is null.</exception>
        public static ScrapeRunSummary From(ScrapeRun run, string? artifactDirectory)
        {
            ArgumentNullException.ThrowIfNull(run);
            return new ScrapeRunSummary(
                run.Id.Value,
                run.Source.Value,
                run.Stage,
                run.Stats,
                run.PauseReason?.ToString(),
                run.RetryAt,
                run.FailureReason,
                artifactDirectory,
                run.OnlyNew
            );
        }
    }
}
