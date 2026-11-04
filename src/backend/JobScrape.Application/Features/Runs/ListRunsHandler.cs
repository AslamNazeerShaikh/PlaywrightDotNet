namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Lists the most recent runs, newest first.
    /// </summary>
    public sealed class ListRunsHandler
    {
        /// <summary>
        /// How many runs the history shows.
        /// </summary>
        public const int Limit = 50;

        // Injected and never mutated.
        private readonly IRunStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListRunsHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public ListRunsHandler(IRunStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Lists up to <paramref name="limit"/> runs, skipping
        /// <paramref name="offset"/> newest first. Coerces
        /// <paramref name="offset"/> to at least 0 and
        /// <paramref name="limit"/> to 1..500 (non-positive resets to
        /// <see cref="Limit"/>), mirroring the controller so direct callers
        /// get the same window; the store still rejects out-of-range values.
        /// </summary>
        /// <param name="offset">How many newest runs to skip.</param>
        /// <param name="limit">How many runs to return.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The runs, newest first, and the total count.</returns>
        public Task<RunListResponse> HandleAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken
        )
        {
            offset = Math.Max(0, offset);
            limit = limit <= 0 ? Limit : Math.Clamp(limit, 1, 500);
            return _store.ListAsync(offset, limit, cancellationToken);
        }
    }
}
