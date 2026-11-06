using JobScrape.Domain.Identity;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="Role"/> aggregates. Role names are
    /// closed (<see cref="RoleName"/>), so lookup by name is exact.
    /// </summary>
    public interface IRoleRepository
    {
        /// <summary>
        /// Loads a role, or null when absent.
        /// </summary>
        /// <param name="id">The role identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The role, or null.</returns>
        Task<Role?> GetAsync(RoleId id, CancellationToken cancellationToken);

        /// <summary>
        /// Loads a role by its closed name, or null when absent.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The role, or null.</returns>
        Task<Role?> GetByNameAsync(RoleName name, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new role; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the role is staged.</returns>
        Task AddAsync(Role role, CancellationToken cancellationToken);

        /// <summary>
        /// Lists every role.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All roles.</returns>
        Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken);
    }
}
