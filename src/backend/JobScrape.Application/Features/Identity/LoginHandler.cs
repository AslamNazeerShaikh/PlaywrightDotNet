using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Authenticates by email and password. Failure counters persist even on
    /// failure, so lockouts survive restarts. Unknown emails report
    /// <c>INVALID_CREDENTIALS</c> like a wrong password, never existence.
    /// </summary>
    public sealed partial class LoginHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly ITokenService _tokens;
        private readonly IUnitOfWork _unitOfWork;
        private readonly LoginPolicy _policy;
        private readonly TimeProvider _clock;
        private readonly ILogger<LoginHandler>? _logger;

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
        /// <param name="logger">Optional logger for dropped roles/keys.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public LoginHandler(
            IUserRepository users,
            IRoleRepository roles,
            ITokenService tokens,
            IUnitOfWork unitOfWork,
            LoginPolicy policy,
            TimeProvider clock,
            ILogger<LoginHandler>? logger = null
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
            _logger = logger;
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
            var permissions = new List<PermissionKey>();
            var warnedRoles = new HashSet<RoleId>();
            var warnedPermissions = new HashSet<PermissionKey>();

            // Dangling role ids (e.g. a role deleted after it was granted)
            // are pruned here so a deleted role cannot replay through a stale
            // token; the revoke is persisted by the existing login save below.
            foreach (var roleId in user.Roles.ToList())
            {
                var role = await _roles.GetAsync(roleId, cancellationToken);
                if (role is null)
                {
                    if (warnedRoles.Add(roleId) && _logger is not null)
                    {
                        LogDroppedRole(_logger, roleId, user.Id);
                    }

                    user.RevokeRole(roleId);
                    continue;
                }

                roles.Add(role);
                foreach (var key in role.PermissionKeys.ToList())
                {
                    if (PermissionCatalog.Contains(key))
                    {
                        permissions.Add(key);
                    }
                    else
                    {
                        if (warnedPermissions.Add(key) && _logger is not null)
                        {
                            LogDroppedPermission(_logger, key, role.Name);
                        }

                        role.Revoke(key);
                    }
                }
            }

            var distinctPermissions = permissions.Distinct().ToList();
            var token = _tokens.CreateToken(user, roles, distinctPermissions, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(new LoginResponse(token, user.MustChangePassword));
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Dropped unknown role id {RoleId} from user {UserId}"
        )]
        private static partial void LogDroppedRole(ILogger logger, RoleId roleId, UserId userId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Dropped unknown permission key {PermissionKey} from role {RoleName}"
        )]
        private static partial void LogDroppedPermission(
            ILogger logger,
            PermissionKey permissionKey,
            RoleName roleName
        );
    }
}
