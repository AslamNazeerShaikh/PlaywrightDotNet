using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Gets one run by id.
    /// </summary>
    public sealed class GetRunHandler
    {
        // Injected and never mutated.
        private readonly IRunStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetRunHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public GetRunHandler(IRunStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Gets the run.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<RunResponse>> HandleAsync(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            if (id == Guid.Empty)
            {
                return Result.Fail<RunResponse>(RunIds.Required());
            }

            var run = await _store.GetAsync(id, cancellationToken);
            return run is null ? Result.Fail<RunResponse>(Errors.RunNotFound(id)) : Result.Ok(run);
        }
    }
}
