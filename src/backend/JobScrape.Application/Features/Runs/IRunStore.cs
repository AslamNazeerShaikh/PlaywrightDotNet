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
        /// Lists the postings a run saw: those of its source whose external ids
        /// its progress recorded. Later runs may refresh the same rows, so the
        /// timestamps alone cannot identify a run's postings. Optional filters
        /// narrow the id set before paging: a location filter matches when any
        /// comma-separated part of the posting's raw location equals it
        /// (case-insensitive); a company filter matches the posting's company
        /// exactly (case-insensitive). Both axes combine with AND; a null,
        /// empty, or all-blank axis is unfiltered.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="offset">How many postings to skip; at least 0.</param>
        /// <param name="limit">How many postings to return; at least 1.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <param name="locations">Location filters; null means unfiltered.</param>
        /// <param name="companies">Company filters; null means unfiltered.</param>
        /// <returns>The postings, or null when the run does not exist.</returns>
        Task<RunPostingListResponse?> ListPostingsAsync(
            Guid runId,
            int offset,
            int limit,
            CancellationToken cancellationToken,
            IReadOnlyList<string>? locations = null,
            IReadOnlyList<string>? companies = null
        );

        /// <summary>
        /// Lists the distinct location parts across the postings a run saw:
        /// each posting's raw location split on commas, trimmed, de-duplicated
        /// case-insensitively (first spelling wins), and sorted
        /// case-insensitively. Values are directly usable as
        /// <c>locations</c> filters.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The locations, an empty list when the run saw none, or null when the run does not exist.</returns>
        Task<IReadOnlyList<string>?> ListPostingLocationsAsync(
            Guid runId,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Lists the distinct companies across the postings a run saw,
        /// de-duplicated case-insensitively (first spelling wins) and sorted
        /// case-insensitively. Values are directly usable as
        /// <c>companies</c> filters.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The companies, an empty list when the run saw none, or null when the run does not exist.</returns>
        Task<IReadOnlyList<string>?> ListPostingCompaniesAsync(
            Guid runId,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Lists the postings a run saw, with the full stored fields for
        /// export: the same id set as <see cref="ListPostingsAsync"/>, most
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
