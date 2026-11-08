using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Creates a user with a temporary password. The account must complete
    /// first login (<see cref="SetupPasswordHandler"/>) before it can sign in.
    /// Every new user is auto-assigned the <see cref="RoleName.Viewer"/> role
    /// in the same transaction, so an account is never role-less; admins can
    /// grant further roles afterwards. A missing Viewer role fails with
    /// <c>ROLE_NOT_FOUND</c> (the seed is expected to have created it).
    /// </summary>
    public sealed class CreateUserHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUserHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the new user. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public CreateUserHandler(
            IUserRepository users,
            IRoleRepository roles,
            IUnitOfWork unitOfWork,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(clock);
            _users = users;
            _roles = roles;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        /// <summary>
        /// Creates the user with the temporary password.
        /// </summary>
        /// <param name="email">The raw email.</param>
        /// <param name="temporaryPassword">The temporary password.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the new user id; <c>EMAIL_TAKEN</c> when the email is
        /// in use, <c>ROLE_NOT_FOUND</c> when the Viewer role is missing, or
        /// <c>VALIDATION_FAILED</c> for a bad email or password.
        /// </returns>
        public async Task<Result<Guid>> HandleAsync(
            string? email,
            string? temporaryPassword,
            CancellationToken cancellationToken
        )
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                var existing = await _users.GetByEmailAsync(email, cancellationToken);
                if (existing is not null)
                {
                    return Result.Fail<Guid>(Errors.EmailTaken(existing.Email));
                }
            }

            PasswordHash hash;
            try
            {
                hash = PasswordHash.Create(temporaryPassword);
            }
            catch (DomainException ex)
            {
                return Result.Fail<Guid>(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["temporaryPassword"] = [ex.Message] }
                    )
                );
            }

            User user;
            try
            {
                user = User.Create(email, hash, _clock.GetUtcNow());
            }
            catch (DomainException ex)
            {
                return Result.Fail<Guid>(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["email"] = [ex.Message] }
                    )
                );
            }

            var viewer = await _roles.GetByNameAsync(RoleName.Viewer, cancellationToken);
            if (viewer is null)
            {
                return Result.Fail<Guid>(Errors.RoleNotFound(RoleName.Viewer.ToString()));
            }

            user.AssignRole(viewer.Id);

            await _users.AddAsync(user, cancellationToken);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
                when (_unitOfWork.IsUniqueViolation(ex)
                    && ex is not OperationCanceledException
                    && !cancellationToken.IsCancellationRequested
                )
            {
                var winner = await _users.GetByEmailAsync(user.Email, cancellationToken);
                if (winner is not null)
                {
                    return Result.Fail<Guid>(Errors.EmailTaken(winner.Email));
                }

                throw;
            }

            return Result.Ok(user.Id.Value);
        }
    }
}
