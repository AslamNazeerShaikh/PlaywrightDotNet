namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Read port for saved search profiles. Implemented in Infrastructure;
    /// the handler depends only on this abstraction.
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
        /// <returns>The profile summary, or null when no profile has that id.</returns>
        Task<SearchProfileResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    }
}
