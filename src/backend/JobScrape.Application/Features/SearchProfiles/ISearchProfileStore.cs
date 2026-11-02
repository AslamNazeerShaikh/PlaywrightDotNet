namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Read port for saved search profiles. Implemented in Infrastructure
    /// with no-tracking queries; handlers depend only on this abstraction.
    /// </summary>
    public interface ISearchProfileStore
    {
        /// <summary>
        /// Returns the profile with the given identifier, or null when absent.
        /// Absence here is not an error: the handler maps null to a
        /// <c>PROFILE_NOT_FOUND</c> failure.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile, or null when no profile has that id.</returns>
        Task<SearchProfileResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profiles; empty when none exist.</returns>
        Task<IReadOnlyList<SearchProfileResponse>> ListAsync(CancellationToken cancellationToken);
    }
}
