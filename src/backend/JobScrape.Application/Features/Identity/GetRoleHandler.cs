using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Reads one role for the admin surface.
    /// </summary>
    public sealed class GetRoleHandler
    {
        private readonly IRoleRepository _roles;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetRoleHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="roles"/> is null.</exception>
        public GetRoleHandler(IRoleRepository roles)
        {
            ArgumentNullException.ThrowIfNull(roles);
            _roles = roles;
        }

        /// <summary>
        /// Gets one role by name.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the role; <c>ROLE_NOT_FOUND</c> when absent.
        /// </returns>
        public async Task<Result<RoleResponse>> HandleAsync(
            RoleName name,
            CancellationToken cancellationToken
        )
        {
            var role = await _roles.GetByNameAsync(name, cancellationToken);
            if (role is null)
            {
                return Result.Fail<RoleResponse>(Errors.RoleNotFound(name.ToString()));
            }

            return Result.Ok(IdentityResponses.ToRoleResponse(role));
        }
    }
}
