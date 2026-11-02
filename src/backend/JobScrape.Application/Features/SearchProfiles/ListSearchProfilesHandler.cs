namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Lists saved search profiles. Listing cannot fail as an expected
    /// outcome, so it returns the list directly rather than a <c>Result</c>.
    /// </summary>
    public sealed class ListSearchProfilesHandler
    {
        // Injected and never mutated.
        private readonly ISearchProfileStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSearchProfilesHandler"/> class.
        /// </summary>
        /// <param name="store">The profile read port.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public ListSearchProfilesHandler(ISearchProfileStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile list.</returns>
        public async Task<SearchProfileListResponse> HandleAsync(
            CancellationToken cancellationToken
        )
        {
            var items = await _store.ListAsync(cancellationToken);
            return new SearchProfileListResponse(items, items.Count);
        }
    }
}
