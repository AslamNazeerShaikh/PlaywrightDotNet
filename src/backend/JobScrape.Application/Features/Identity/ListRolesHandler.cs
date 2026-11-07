using JobScrape.Application.Abstractions;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Lists roles for the admin surface with granted permission keys.
    /// </summary>
    public sealed class ListRolesHandler
    {
        private readonly IRoleRepository _roles;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListRolesHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="roles"/> is null.</exception>
        public ListRolesHandler(IRoleRepository roles)
        {
            ArgumentNullException.ThrowIfNull(roles);
            _roles = roles;
        }

        /// <summary>
        /// Lists every role.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All roles, in store order.</returns>
        public async Task<IReadOnlyList<RoleResponse>> HandleAsync(
            CancellationToken cancellationToken
        )
        {
            var roles = await _roles.ListAsync(cancellationToken);
            return roles.Select(IdentityResponses.ToRoleResponse).ToList();
        }
    }
}
