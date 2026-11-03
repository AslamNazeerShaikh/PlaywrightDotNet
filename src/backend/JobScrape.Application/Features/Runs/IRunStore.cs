namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Read-side access to runs and the postings they saw. Implementations
    /// are no-tracking queries; nothing read here is written back.
    /// </summary>
    public interface IRunStore
    {
        /// <summary>
        /// Gets one run.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run, or null when it does not exist.</returns>
        Task<RunResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the newest runs.
        /// </summary>
        /// <param name="offset">How many newest runs to skip; at least 0.</param>
        /// <param name="limit">How many runs to return; at least 1.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>Up to <paramref name="limit"/> runs, newest first, and the total count.</returns>
        Task<RunListResponse> ListAsync(int offset, int limit, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the postings a run saw: those of its source last seen
        /// between its start and its finish (or now, while it runs). Runs
        /// execute one at a time, so the window identifies the run's postings.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="offset">How many postings to skip; at least 0.</param>
        /// <param name="limit">How many postings to return; at least 1.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The postings, or null when the run does not exist.</returns>
        Task<RunPostingListResponse?> ListPostingsAsync(
            Guid runId,
            int offset,
            int limit,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Lists the postings a run saw, with the full stored fields for
        /// export: the same window as <see cref="ListPostingsAsync"/>, most
        /// recently seen first.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="limit">How many postings to return; at least 1.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The postings, or null when the run does not exist.</returns>
        Task<IReadOnlyList<ExportPosting>?> ListExportPostingsAsync(
            Guid runId,
            int limit,
            CancellationToken cancellationToken
        );
    }
}
