namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Read port for browser profiles. Implementations read without
    /// tracking and evaluate <c>CanLaunch</c> against the injected clock.
    /// </summary>
    public interface ISessionStore
    {
        /// <summary>
        /// Returns the profile with the given identifier, or null when absent.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null.</returns>
        Task<SessionResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profiles; empty when none exist.</returns>
        Task<IReadOnlyList<SessionResponse>> ListAsync(CancellationToken cancellationToken);
    }
}
