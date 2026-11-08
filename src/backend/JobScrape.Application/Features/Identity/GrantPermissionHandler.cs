using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Grants a catalog permission to a role.
    /// </summary>
    public sealed class GrantPermissionHandler
    {
        private readonly IRoleRepository _roles;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrantPermissionHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="users">The user port for token invalidation. Must not be null.</param>
        /// <param name="unitOfWork">Commits the grant. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public GrantPermissionHandler(
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
        /// Grants the permission to the role.
        /// </summary>
        /// <param name="name">The closed role name.</param>
        /// <param name="permissionKey">The raw permission key.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>VALIDATION_FAILED</c> for a malformed key,
        /// <c>UNKNOWN_PERMISSION</c> outside the catalog,
        /// <c>ROLE_NOT_FOUND</c>, or <c>PERMISSION_ALREADY_GRANTED</c>.
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

            try
            {
                role.Grant(key);
            }
            catch (DomainException)
            {
                return Result.Fail(Errors.PermissionAlreadyGranted(key.Value));
            }

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
