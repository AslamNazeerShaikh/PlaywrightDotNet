using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Lists the postings one run saw.
    /// </summary>
    public sealed class ListRunPostingsHandler
    {
        /// <summary>
        /// How many postings the run detail shows.
        /// </summary>
        public const int Limit = 200;

        // Injected and never mutated.
        private readonly IRunStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListRunPostingsHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public ListRunPostingsHandler(IRunStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Lists up to <see cref="Limit"/> postings for the run.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The postings; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<RunPostingListResponse>> HandleAsync(
            Guid runId,
            CancellationToken cancellationToken
        )
        {
            if (runId == Guid.Empty)
            {
                return Result.Fail<RunPostingListResponse>(RunIds.Required());
            }

            var postings = await _store.ListPostingsAsync(runId, Limit, cancellationToken);
            return postings is null
                ? Result.Fail<RunPostingListResponse>(Errors.RunNotFound(runId))
                : Result.Ok(postings);
        }
    }
}
