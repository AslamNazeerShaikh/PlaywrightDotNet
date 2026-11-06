using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Revokes a role. Revoking the last admin assignment is refused so the
    /// system always keeps one administrator.
    /// </summary>
    public sealed class RevokeRoleHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="RevokeRoleHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the revocation. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RevokeRoleHandler(
            IUserRepository users,
            IRoleRepository roles,
            IUnitOfWork unitOfWork
        )
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _users = users;
            _roles = roles;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Revokes the role from the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="roleName">The role to revoke.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>VALIDATION_FAILED</c> for an empty user id,
        /// <c>USER_NOT_FOUND</c>, <c>ROLE_NOT_FOUND</c>, or
        /// <c>LAST_ADMIN_REQUIRED</c> when this is the last admin.
        /// </returns>
        public async Task<Result> HandleAsync(
            Guid userId,
            RoleName roleName,
            CancellationToken cancellationToken
        )
        {
            UserId id;
            try
            {
                id = UserId.From(userId);
            }
            catch (DomainException ex)
            {
                return Result.Fail(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["userId"] = [ex.Message] }
                    )
                );
            }

            var user = await _users.GetAsync(id, cancellationToken);
            if (user is null)
            {
                return Result.Fail(Errors.UserNotFound(userId.ToString()));
            }

            var role = await _roles.GetByNameAsync(roleName, cancellationToken);
            if (role is null)
            {
                return Result.Fail(Errors.RoleNotFound(roleName.ToString()));
            }

            if (role.Name == RoleName.Admin && user.Roles.Contains(role.Id))
            {
                var users = await _users.ListAsync(cancellationToken);
                var anotherAdmin = users.Any(other =>
                    other.Id != user.Id && other.Roles.Contains(role.Id)
                );
                if (!anotherAdmin)
                {
                    return Result.Fail(Errors.LastAdminRequired());
                }
            }

            user.RevokeRole(role.Id);
            user.BumpTokenVersion();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
    }
}
