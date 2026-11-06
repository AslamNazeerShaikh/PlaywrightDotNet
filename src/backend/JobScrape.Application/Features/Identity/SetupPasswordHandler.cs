using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Completes first login by replacing the seeded or temporary password.
    /// Verifies the current secret with <see cref="PasswordHash.Verify"/>
    /// directly: <c>Authenticate</c> refuses while a reset is pending, and
    /// this handler is exactly that reset.
    /// </summary>
    public sealed class SetupPasswordHandler
    {
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupPasswordHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the new password. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SetupPasswordHandler(
            IUserRepository users,
            IUnitOfWork unitOfWork,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(clock);
            _users = users;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        /// <summary>
        /// Verifies the current secret and replaces it with a new password.
        /// </summary>
        /// <param name="email">The raw email.</param>
        /// <param name="currentSecret">The seeded or temporary password.</param>
        /// <param name="newPassword">The operator-chosen password.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>PASSWORD_SETUP_COMPLETE</c> when first login already
        /// finished or the email is unknown (one code closes the account
        /// enumeration oracle; pending accounts stay rate-limited),
        /// <c>INVALID_CREDENTIALS</c> for a wrong secret, or
        /// <c>VALIDATION_FAILED</c> when the new password misses criteria.
        /// </returns>
        public async Task<Result> HandleAsync(
            string? email,
            string? currentSecret,
            string? newPassword,
            CancellationToken cancellationToken
        )
        {
            var user = string.IsNullOrWhiteSpace(email)
                ? null
                : await _users.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                return Result.Fail(Errors.PasswordSetupComplete());
            }

            if (!user.MustChangePassword)
            {
                return Result.Fail(Errors.PasswordSetupComplete());
            }

            if (!user.PasswordHash.Verify(currentSecret))
            {
                return Result.Fail(Errors.InvalidCredentials());
            }

            PasswordHash hash;
            try
            {
                hash = PasswordHash.Create(newPassword);
            }
            catch (DomainException ex)
            {
                return Result.Fail(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["newPassword"] = [ex.Message] }
                    )
                );
            }

            user.CompleteFirstLogin(hash, _clock.GetUtcNow());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
    }
}
