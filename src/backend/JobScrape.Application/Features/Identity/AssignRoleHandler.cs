using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Assigns and revokes roles. Revoking the admin role from the last
    /// admin is refused: at least one other user must hold it. Duplicate
    /// assignment is a conflict; revoking an unassigned role converges.
    /// </summary>
    public sealed class AssignRoleHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssignRoleHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the assignment. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public AssignRoleHandler(
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
        /// Assigns the role to the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="roleName">The role to assign.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>VALIDATION_FAILED</c> for an empty user id,
        /// <c>USER_NOT_FOUND</c>, <c>ROLE_NOT_FOUND</c>, or
        /// <c>ROLE_ALREADY_ASSIGNED</c> on a duplicate.
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

            try
            {
                user.AssignRole(role.Id);
                user.BumpTokenVersion();
            }
            catch (DomainException)
            {
                return Result.Fail(Errors.RoleAlreadyAssigned(roleName.ToString()));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
    }
}
