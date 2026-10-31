using JobScrape.Application.Results;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Explicit use-case handler for profile lookup. Called directly by the
    /// API controller; no mediator, no bus. Returns a <see cref="Result{T}"/>
    /// because absence and bad ids are expected operational outcomes.
    /// </summary>
    public sealed class GetSearchProfileHandler
    {
        // Injected, never mutated: the handler stays stateless and safe to
        // register as a transient that concurrent requests share nothing through.
        private readonly ISearchProfileStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSearchProfileHandler"/> class.
        /// </summary>
        /// <param name="store">The profile read port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public GetSearchProfileHandler(ISearchProfileStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Returns the profile summary for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the profile summary; failure with
        /// <c>VALIDATION_FAILED</c> when <paramref name="id"/> is empty, or
        /// <c>PROFILE_NOT_FOUND</c> when no profile has that id.
        /// </returns>
        public async Task<Result<SearchProfileResponse>> HandleAsync(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            // Empty guids are programmer-facing validation failures, not
            // storage misses: reject before touching the store.
            if (id == Guid.Empty)
            {
                return Result.Fail<SearchProfileResponse>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]> { ["id"] = ["A profile id is required."] }
                    )
                );
            }

            var profile = await _store.GetAsync(id, cancellationToken);
            if (profile is null)
            {
                return Result.Fail<SearchProfileResponse>(Errors.ProfileNotFound(id));
            }

            return Result.Ok(profile);
        }
    }
}
