using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Looks up one browser profile.
    /// </summary>
    public sealed class GetSessionHandler
    {
        // Injected and never mutated.
        private readonly ISessionStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSessionHandler"/> class.
        /// </summary>
        /// <param name="store">The session read port.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public GetSessionHandler(ISessionStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Returns the profile for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the profile; <c>VALIDATION_FAILED</c> when
        /// <paramref name="id"/> is empty; <c>SESSION_NOT_FOUND</c> when absent.
        /// </returns>
        public async Task<Result<SessionResponse>> HandleAsync(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            if (id == Guid.Empty)
            {
                return Result.Fail<SessionResponse>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]> { ["id"] = ["A session id is required."] }
                    )
                );
            }

            var session = await _store.GetAsync(id, cancellationToken);
            return session is null
                ? Result.Fail<SessionResponse>(Errors.SessionNotFound(id))
                : Result.Ok(session);
        }
    }
}
