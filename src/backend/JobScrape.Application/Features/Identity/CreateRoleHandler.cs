using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Creates roles and changes their permission grants. Permission keys
    /// must parse and belong to <see cref="PermissionCatalog"/>; grants are
    /// sets, so duplicates conflict while revoking an absent grant converges.
    /// </summary>
    public sealed class CreateRoleHandler
    {
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateRoleHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the new role. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public CreateRoleHandler(IRoleRepository roles, IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _roles = roles;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Creates the role with no granted permissions.
        /// </summary>
        /// <param name="name">The closed role name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the new role id; <c>ROLE_NAME_TAKEN</c> when it exists.
        /// </returns>
        public async Task<Result<Guid>> HandleAsync(
            RoleName name,
            CancellationToken cancellationToken
        )
        {
            var existing = await _roles.GetByNameAsync(name, cancellationToken);
            if (existing is not null)
            {
                return Result.Fail<Guid>(Errors.RoleNameTaken(name.ToString()));
            }

            var role = Role.Create(RoleId.New(), name);
            await _roles.AddAsync(role, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(role.Id.Value);
        }
    }
}
