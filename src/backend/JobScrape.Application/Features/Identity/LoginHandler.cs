using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Authenticates by email and password. Failure counters persist even on
    /// failure, so lockouts survive restarts. Unknown emails report
    /// <c>INVALID_CREDENTIALS</c> like a wrong password, never existence.
    /// </summary>
    public sealed class LoginHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly ITokenService _tokens;
        private readonly IUnitOfWork _unitOfWork;
        private readonly LoginPolicy _policy;
        private readonly TimeProvider _clock;

        // Fixed hash used to make the unknown-email path pay one PBKDF2
        // verification, so it cannot be distinguished from a known account
        // by timing.
        private static readonly PasswordHash DummyHash = PasswordHash.Restore(
            new byte[16],
            new byte[32],
            210_000
        );

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="tokens">The token port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the failure counters. Must not be null.</param>
        /// <param name="policy">The lockout policy. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public LoginHandler(
            IUserRepository users,
            IRoleRepository roles,
            ITokenService tokens,
            IUnitOfWork unitOfWork,
            LoginPolicy policy,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(tokens);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(clock);
            _users = users;
            _roles = roles;
            _tokens = tokens;
            _unitOfWork = unitOfWork;
            _policy = policy;
            _clock = clock;
        }

        /// <summary>
        /// Authenticates the credentials and issues a bearer token.
        /// </summary>
        /// <param name="email">The raw email.</param>
        /// <param name="password">The clear-text candidate.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the token; <c>INVALID_CREDENTIALS</c> for an
        /// unknown email, a pending first login, or a wrong password, or
        /// <c>ACCOUNT_LOCKED</c> while locked out.
        /// </returns>
        public async Task<Result<LoginResponse>> HandleAsync(
            string? email,
            string? password,
            CancellationToken cancellationToken
        )
        {
            var user = string.IsNullOrWhiteSpace(email)
                ? null
                : await _users.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                // Cost one verification so the unknown-email path is not
                // cheaper than a known account with a wrong password.
                DummyHash.Verify(password);
                return Result.Fail<LoginResponse>(Errors.InvalidCredentials());
            }

            var now = _clock.GetUtcNow();
            try
            {
                user.Authenticate(password, now, _policy.MaxFailedAttempts, _policy.Lockout);
            }
            catch (DomainException ex)
                when (ex.Message.StartsWith("ACCOUNT_LOCKED", StringComparison.Ordinal))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Fail<LoginResponse>(Errors.AccountLocked(user.LockedUntilUtc));
            }
            catch (DomainException ex)
                when (ex.Message.StartsWith("INVALID_CREDENTIALS", StringComparison.Ordinal))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Fail<LoginResponse>(Errors.InvalidCredentials());
            }

            var roles = new List<Domain.Identity.Role>();
            foreach (var roleId in user.Roles)
            {
                var role = await _roles.GetAsync(roleId, cancellationToken);
                if (role is not null)
                {
                    roles.Add(role);
                }
            }

            var permissions = roles.SelectMany(role => role.PermissionKeys).Distinct().ToList();
            var token = _tokens.CreateToken(user, roles, permissions, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(new LoginResponse(token, user.MustChangePassword));
        }
    }
}
