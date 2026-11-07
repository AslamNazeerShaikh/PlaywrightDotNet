using System.Security.Cryptography;
using System.Text;
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

        // Fixed hash used to make the unknown-email path pay one PBKDF2
        // verification, so it cannot be distinguished from a pending account
        // with a wrong secret by timing.
        private static readonly PasswordHash DummyHash = PasswordHash.Restore(
            new byte[16],
            new byte[32],
            210_000
        );

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
        /// Success; <c>INVALID_CREDENTIALS</c> for an unknown email or a
        /// wrong secret, <c>PASSWORD_SETUP_COMPLETE</c> when first login
        /// already finished, <c>ACCOUNT_LOCKED</c> while locked out, or
        /// <c>VALIDATION_FAILED</c> when the new password misses criteria
        /// or matches the current secret.
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
                // Cost one verification so the unknown-email path is not
                // cheaper than a pending account with a wrong secret.
                DummyHash.Verify(currentSecret);
                return Result.Fail(Errors.InvalidCredentials());
            }

            // Cost one verification before branching on account state, so a
            // completed account is not distinguishable from a pending account
            // with a wrong secret (or an unknown account) by timing.
            var secretMatches = user.PasswordHash.Verify(currentSecret);

            if (!user.MustChangePassword)
            {
                return Result.Fail(Errors.PasswordSetupComplete());
            }

            // A wrong secret is always 401, even on a locked account; only a
            // correct secret on a locked account surfaces ACCOUNT_LOCKED.
            if (!secretMatches)
            {
                return Result.Fail(Errors.InvalidCredentials());
            }

            var now = _clock.GetUtcNow();
            if (user.IsLockedOut(now))
            {
                return Result.Fail(Errors.AccountLocked(user.LockedUntilUtc));
            }

            if (SecretsEqual(currentSecret, newPassword))
            {
                return Result.Fail(
                    Errors.ValidationFailed(
                        "The new password must differ from the current password.",
                        new Dictionary<string, string[]>
                        {
                            ["newPassword"] =
                            [
                                "The new password must differ from the current password.",
                            ],
                        }
                    )
                );
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

            user.CompleteFirstLogin(hash, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }

        private static bool SecretsEqual(string? left, string? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            if (leftBytes.Length != rightBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}
