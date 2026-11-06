using JobScrape.Domain.Abstractions;

namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// Aggregate root for one login account: normalized email, password
    /// hash, first-login state, lockout counters, and role assignments.
    /// Authentication failures are domain failures: <see cref="Authenticate"/>
    /// throws a <see cref="DomainException"/> whose message starts with a
    /// machine-readable code (<c>PASSWORD_RESET_REQUIRED</c>,
    /// <c>ACCOUNT_LOCKED</c>, <c>INVALID_CREDENTIALS</c>) so the application
    /// layer can map it to a 401/403 response without parsing internals.
    /// </summary>
    public sealed class User : AggregateRoot
    {
        private readonly List<RoleId> _roles;

        /// <summary>
        /// Initializes a new instance. Private so users are created through
        /// <see cref="Create"/>, <see cref="CreateSeedAdmin"/>, or rehydration
        /// at the persistence edge.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="email">The normalized email.</param>
        /// <param name="hash">The password hash.</param>
        /// <param name="mustChangePassword">Whether first login must set a new password.</param>
        /// <param name="createdAt">When the account was created.</param>
        private User(
            UserId id,
            string email,
            PasswordHash hash,
            bool mustChangePassword,
            DateTimeOffset createdAt
        )
        {
            Id = id;
            Email = email;
            PasswordHash = hash;
            MustChangePassword = mustChangePassword;
            CreatedAt = createdAt;
            _roles = [];
        }

        /// <summary>
        /// Initializes an empty instance for Entity Framework rehydration.
        /// Private so production code uses <see cref="Create"/> instead.
        /// </summary>
        private User()
        {
            Email = string.Empty;
            PasswordHash = null!;
            _roles = [];
        }

        /// <summary>
        /// Gets the user identifier.
        /// </summary>
        public UserId Id { get; private set; }

        /// <summary>
        /// Gets the email, trimmed and normalized to lowercase invariant.
        /// </summary>
        public string Email { get; private set; }

        /// <summary>
        /// Gets the current password hash.
        /// </summary>
        public PasswordHash PasswordHash { get; private set; }

        /// <summary>
        /// Gets whether the next login must set a new password before any
        /// other authentication can succeed.
        /// </summary>
        public bool MustChangePassword { get; private set; }

        /// <summary>
        /// Gets the consecutive failed login count since the last success.
        /// </summary>
        public int FailedAttempts { get; private set; }

        /// <summary>
        /// Gets when the lockout ends, if the account is locked.
        /// </summary>
        public DateTimeOffset? LockedUntilUtc { get; private set; }

        /// <summary>
        /// Gets the token version. Every password change and lockout bumps
        /// it; bearer tokens carry the version at issuance, so a mismatch
        /// revokes outstanding tokens.
        /// </summary>
        public int TokenVersion { get; private set; }

        /// <summary>
        /// Gets when the account was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Gets the assigned roles, in assignment order.
        /// </summary>
        public IReadOnlyList<RoleId> Roles => _roles.AsReadOnly();

        /// <summary>
        /// Creates a user that must set its password on first login. Starts
        /// with no roles: the application layer assigns roles after creation.
        /// </summary>
        /// <param name="email">The user email. Trimmed and lowercased.</param>
        /// <param name="hash">The initial password hash.</param>
        /// <param name="now">The creation time.</param>
        /// <returns>The user, awaiting first login.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when <paramref name="email"/> is blank or malformed.</exception>
        public static User Create(string? email, PasswordHash hash, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(hash);
            return new User(UserId.New(), NormalizeEmail(email), hash, true, now);
        }

        /// <summary>
        /// Creates the seeded default admin, which must set its password on
        /// first login. Starts with no roles: the application layer assigns
        /// the admin role after seeding.
        /// </summary>
        /// <param name="email">The admin email. Trimmed and lowercased.</param>
        /// <param name="hash">The initial password hash.</param>
        /// <param name="now">The seeding time.</param>
        /// <returns>The seed admin, awaiting first login.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when <paramref name="email"/> is blank or malformed.</exception>
        public static User CreateSeedAdmin(string? email, PasswordHash hash, DateTimeOffset now)
        {
            return Create(email, hash, now);
        }

        /// <summary>
        /// Completes the first login by replacing the seeded password.
        /// </summary>
        /// <param name="newHash">The operator-chosen password hash.</param>
        /// <param name="now">The completion time; recorded by clearing any lockout as of then.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newHash"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when first login was already completed.</exception>
        public void CompleteFirstLogin(PasswordHash newHash, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(newHash);
            if (!MustChangePassword)
            {
                throw new DomainException("First-login setup is already complete.");
            }

            PasswordHash = newHash;
            MustChangePassword = false;
            FailedAttempts = 0;
            LockedUntilUtc = null;
            TokenVersion++;
        }

        /// <summary>
        /// Replaces the password outside first login, clearing failures and
        /// any lockout. The caller verifies the current secret first.
        /// </summary>
        /// <param name="newHash">The operator-chosen password hash.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newHash"/> is null.</exception>
        public void ChangePassword(PasswordHash newHash)
        {
            ArgumentNullException.ThrowIfNull(newHash);
            PasswordHash = newHash;
            MustChangePassword = false;
            FailedAttempts = 0;
            LockedUntilUtc = null;
            TokenVersion++;
        }

        /// <summary>
        /// Authenticates a clear-text candidate. Success resets the failure
        /// counters; failure records itself and locks past the limit.
        /// </summary>
        /// <param name="password">The clear-text candidate.</param>
        /// <param name="now">The attempt time.</param>
        /// <param name="maxAttempts">Failures allowed before lockout. Must be positive.</param>
        /// <param name="lockout">How long a lockout lasts.</param>
        /// <exception cref="DomainException">
        /// Thrown with <c>PASSWORD_RESET_REQUIRED</c> when first login is
        /// pending, <c>ACCOUNT_LOCKED</c> while locked out, or
        /// <c>INVALID_CREDENTIALS</c> when the password does not match.
        /// </exception>
        public void Authenticate(
            string? password,
            DateTimeOffset now,
            int maxAttempts,
            TimeSpan lockout
        )
        {
            if (MustChangePassword)
            {
                throw new DomainException(
                    "PASSWORD_RESET_REQUIRED: First login must set a new password."
                );
            }

            if (IsLockedOut(now))
            {
                throw new DomainException("ACCOUNT_LOCKED: Too many failed logins.");
            }

            if (PasswordHash.Verify(password))
            {
                RecordSuccessfulLogin();
                return;
            }

            RecordFailedLogin(now, maxAttempts, lockout);
            throw new DomainException("INVALID_CREDENTIALS: Password does not match.");
        }

        /// <summary>
        /// Records a failed login, locking the account past the limit.
        /// </summary>
        /// <param name="now">The attempt time.</param>
        /// <param name="maxAttempts">Failures allowed before lockout. Must be positive.</param>
        /// <param name="lockout">How long a lockout lasts once triggered.</param>
        /// <exception cref="DomainException">Thrown when <paramref name="maxAttempts"/> is not positive.</exception>
        public void RecordFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockout)
        {
            if (maxAttempts < 1)
            {
                throw new DomainException("Max attempts must be positive.");
            }

            FailedAttempts++;
            if (FailedAttempts >= maxAttempts)
            {
                LockedUntilUtc = now + lockout;
                TokenVersion++;
            }
        }

        /// <summary>
        /// Records a successful login, clearing failures and any lockout.
        /// </summary>
        public void RecordSuccessfulLogin()
        {
            FailedAttempts = 0;
            LockedUntilUtc = null;
        }

        /// <summary>
        /// Reports whether the account is locked at <paramref name="now"/>.
        /// A lockout whose time has passed no longer locks.
        /// </summary>
        /// <param name="now">The time to check.</param>
        /// <returns>True while locked out.</returns>
        public bool IsLockedOut(DateTimeOffset now)
        {
            return LockedUntilUtc.HasValue && now < LockedUntilUtc.Value;
        }

        /// <summary>
        /// Invalidates issued tokens without changing anything else: role
        /// and permission changes call this so stale claims die at once.
        /// </summary>
        public void BumpTokenVersion()
        {
            TokenVersion++;
        }

        /// <summary>
        /// Assigns a role. Duplicate assignment is rejected: the roles list
        /// is a set in assignment order.
        /// </summary>
        /// <param name="roleId">The role to assign.</param>
        /// <exception cref="DomainException">Thrown when the role is already assigned.</exception>
        public void AssignRole(RoleId roleId)
        {
            if (_roles.Contains(roleId))
            {
                throw new DomainException($"Role '{roleId}' is already assigned.");
            }

            _roles.Add(roleId);
        }

        /// <summary>
        /// Revokes a role. Revoking a role that was never assigned is a
        /// no-op, so retries converge.
        /// </summary>
        /// <param name="roleId">The role to revoke.</param>
        public void RevokeRole(RoleId roleId)
        {
            _roles.Remove(roleId);
        }

        /// <summary>
        /// Trims and lowercases an email, rejecting blank or malformed input.
        /// </summary>
        /// <param name="email">The raw email.</param>
        /// <returns>The normalized email.</returns>
        /// <exception cref="DomainException">Thrown when blank or malformed.</exception>
        private static string NormalizeEmail(string? email)
        {
            var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
            var at = normalized.IndexOf('@', StringComparison.Ordinal);
            var valid =
                normalized.Length > 0
                && at > 0
                && at == normalized.LastIndexOf('@')
                && at < normalized.Length - 1
                && normalized[(at + 1)..].Contains('.', StringComparison.Ordinal)
                && !normalized.Contains(' ', StringComparison.Ordinal);
            if (!valid)
            {
                throw new DomainException("Email address is invalid.");
            }

            return normalized;
        }
    }
}
