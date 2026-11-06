using System.Security.Cryptography;
using System.Text;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Changes the password for an account past first login. Verifies the
    /// current secret first; a mismatch reports <c>INVALID_CREDENTIALS</c>
    /// like a bad login, never which side was wrong.
    /// </summary>
    public sealed class ChangePasswordHandler
    {
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;

        // Fixed hash used to make the unknown-email path pay one PBKDF2
        // verification, so it cannot be distinguished from a known account
        // with a wrong secret by timing.
        private static readonly PasswordHash DummyHash = PasswordHash.Restore(
            new byte[16],
            new byte[32],
            210_000
        );

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangePasswordHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the new password. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public ChangePasswordHandler(IUserRepository users, IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _users = users;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Verifies the current secret and replaces it with the new password.
        /// </summary>
        /// <param name="email">The account email, from the bearer token.</param>
        /// <param name="currentPassword">The current clear-text secret.</param>
        /// <param name="newPassword">The operator-chosen password.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success; <c>INVALID_CREDENTIALS</c> for an unknown email or wrong
        /// secret, or <c>VALIDATION_FAILED</c> when the new password misses
        /// the criteria.
        /// </returns>
        public async Task<Result> HandleAsync(
            string? email,
            string? currentPassword,
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
                // cheaper than a known account with a wrong secret.
                DummyHash.Verify(currentPassword);
                return Result.Fail(Errors.InvalidCredentials());
            }

            if (!user.PasswordHash.Verify(currentPassword))
            {
                return Result.Fail(Errors.InvalidCredentials());
            }

            if (SecretsEqual(currentPassword, newPassword))
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

            user.ChangePassword(hash);
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
