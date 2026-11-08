using JobScrape.Domain.Identity;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="User"/> aggregates. Email uniqueness
    /// is enforced by the store and surfaces as a conflict.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Loads a user by normalized email, or null when absent. The lookup
        /// normalizes the input the same way the domain does.
        /// </summary>
        /// <param name="email">The raw email.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The user, or null.</returns>
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

        /// <summary>
        /// Loads a user, or null when absent.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The user, or null.</returns>
        Task<User?> GetAsync(UserId id, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new user; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the user is staged.</returns>
        Task AddAsync(User user, CancellationToken cancellationToken);

        /// <summary>
        /// Lists every user. Used for the last-admin guard, not for paging.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All users.</returns>
        Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken);
    }
}
