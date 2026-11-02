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
        /// Lists up to <see cref="Limit"/> runs.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The runs, newest first, and the total count.</returns>
        public Task<RunListResponse> HandleAsync(CancellationToken cancellationToken)
        {
            return _store.ListAsync(Limit, cancellationToken);
        }
    }
}
