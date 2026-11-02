namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Lists browser profiles. Listing cannot fail as an expected outcome,
    /// so it returns the list directly rather than a <c>Result</c>.
    /// </summary>
    public sealed class ListSessionsHandler
    {
        // Injected and never mutated.
        private readonly ISessionStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSessionsHandler"/> class.
        /// </summary>
        /// <param name="store">The session read port.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public ListSessionsHandler(ISessionStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile list.</returns>
        public async Task<SessionListResponse> HandleAsync(CancellationToken cancellationToken)
        {
            var items = await _store.ListAsync(cancellationToken);
            return new SessionListResponse(items, items.Count);
        }
    }
}
