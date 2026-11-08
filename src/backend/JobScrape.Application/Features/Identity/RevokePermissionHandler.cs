using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Revokes a catalog permission from a role. Revoking a grant the role
    /// never had is a no-op, so retries converge.
    /// </summary>
    public sealed class RevokePermissionHandler
    {
        private readonly IRoleRepository _roles;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="RevokePermissionHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="users">The user port for token invalidation. Must not be null.</param>
        /// <param name="unitOfWork">Commits the revocation. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RevokePermissionHandler(
            IRoleRepository roles,
            IUserRepository users,
            IUnitOfWork unitOfWork
        )
        {
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _roles = roles;
            _users = users;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Revokes the permission from the role.
        /// </summary>
        /// <param name="name">The closed role name.</param>
        /// <param name="permissionKey">The raw permission key.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>VALIDATION_FAILED</c> for a malformed key,
        /// <c>UNKNOWN_PERMISSION</c> outside the catalog,
        /// <c>ROLE_NOT_FOUND</c>, or <c>ROLE_PROTECTED</c> when the role is
        /// the admin role.
        /// </returns>
        public async Task<Result> HandleAsync(
            RoleName name,
            string? permissionKey,
            CancellationToken cancellationToken
        )
        {
            PermissionKey key;
            try
            {
                key = PermissionKey.From(permissionKey);
            }
            catch (DomainException ex)
            {
                return Result.Fail(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["permissionKey"] = [ex.Message] }
                    )
                );
            }

            if (!PermissionCatalog.Contains(key))
            {
                return Result.Fail(Errors.UnknownPermission(key.Value));
            }

            var role = await _roles.GetByNameAsync(name, cancellationToken);
            if (role is null)
            {
                return Result.Fail(Errors.RoleNotFound(name.ToString()));
            }

            if (role.Name == RoleName.Admin)
            {
                // The admin role keeps the full catalog by definition.
                // Concurrent double-revoke of the last admin assignment is
                // accepted risk: SQLite is a single writer, so the check and
                // the write serialize without a distributed lock.
                return Result.Fail(Errors.RoleProtected());
            }

            role.Revoke(key);

            // Permission claims ride inside issued tokens: bump every
            // holder so stale grants die at once.
            var holders = await _users.ListAsync(cancellationToken);
            foreach (var holder in holders.Where(user => user.Roles.Contains(role.Id)))
            {
                holder.BumpTokenVersion();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
    }
}
