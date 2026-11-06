using System.Threading;
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
        // Single-instance guard only: the host runs as a single process
        // (Program.cs:66-68, docs/02-architecture.md:17), so a process-local
        // semaphore serializes the last-admin check across concurrent
        // requests. It does not coordinate across processes.
        private static readonly SemaphoreSlim _adminGuard = new(1, 1);

        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICallerContext _caller;

        /// <summary>
        /// Initializes a new instance of the <see cref="RevokeRoleHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the revocation. Must not be null.</param>
        /// <param name="caller">The calling actor. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RevokeRoleHandler(
            IUserRepository users,
            IRoleRepository roles,
            IUnitOfWork unitOfWork,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(caller);
            _users = users;
            _roles = roles;
            _unitOfWork = unitOfWork;
            _caller = caller;
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

            if (id.Value.ToString() == _caller.ActorId)
            {
                return Result.Fail(Errors.AccessDenied());
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

            await _adminGuard.WaitAsync(cancellationToken);
            try
            {
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
            finally
            {
                _adminGuard.Release();
            }
        }
    }
}
