using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Ensures a closed role row exists. The three role names are seeded at
    /// startup, so a repeat request reports the existing row instead of a
    /// conflict; only a host whose seed never ran inserts. Permission grants
    /// are a separate use case.
    /// </summary>
    public sealed class EnsureRoleHandler
    {
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnsureRoleHandler"/> class.
        /// </summary>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits a missing role. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public EnsureRoleHandler(IRoleRepository roles, IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _roles = roles;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Returns the role, creating it with no granted permissions when it
        /// is missing.
        /// </summary>
        /// <param name="name">The closed role name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the role id and whether this call created the row.
        /// </returns>
        public async Task<Result<RoleEnsureOutcome>> HandleAsync(
            RoleName name,
            CancellationToken cancellationToken
        )
        {
            var existing = await _roles.GetByNameAsync(name, cancellationToken);
            if (existing is not null)
            {
                return Result.Ok(new RoleEnsureOutcome(existing.Id.Value, Created: false));
            }

            var role = Role.Create(RoleId.New(), name);
            await _roles.AddAsync(role, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(new RoleEnsureOutcome(role.Id.Value, Created: true));
        }
    }

    /// <summary>
    /// The result of ensuring a role exists.
    /// </summary>
    /// <param name="Id">The role identifier.</param>
    /// <param name="Created">True when this call inserted the row.</param>
    public sealed record RoleEnsureOutcome(Guid Id, bool Created);
}
