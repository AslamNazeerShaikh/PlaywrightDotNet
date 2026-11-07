using System.Security.Cryptography;

namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// A password hashed with PBKDF2-SHA256 and a random per-password salt.
    /// The criteria factory <see cref="Create"/> is the only way to hash a
    /// new password; <see cref="Restore"/> rehydrates stored bytes.
    /// Verification uses a constant-time comparison.
    /// </summary>
    public sealed class PasswordHash : IEquatable<PasswordHash>
    {
        // Sizes and work factor for new hashes. Stored hashes carry their
        // own iteration count, so raising this never breaks verification.
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int DefaultIterations = 210_000;

        private readonly byte[] _salt;
        private readonly byte[] _hash;

        /// <summary>
        /// Initializes a new instance from already-validated parts.
        /// </summary>
        /// <param name="salt">The random salt. Copied, never aliased.</param>
        /// <param name="hash">The derived key. Copied, never aliased.</param>
        /// <param name="iterations">The PBKDF2 iteration count.</param>
        private PasswordHash(byte[] salt, byte[] hash, int iterations)
        {
            _salt = salt;
            _hash = hash;
            Iterations = iterations;
        }

        /// <summary>
        /// Gets the PBKDF2 iteration count this hash was derived with.
        /// </summary>
        public int Iterations { get; }

        /// <summary>
        /// Gets a copy of the random salt.
        /// </summary>
        public byte[] Salt
        {
            get { return (byte[])_salt.Clone(); }
        }

        /// <summary>
        /// Gets a copy of the derived key.
        /// </summary>
        public byte[] Hash
        {
            get { return (byte[])_hash.Clone(); }
        }

        /// <summary>
        /// Validates a new password against the criteria and hashes it with
        /// a fresh random salt.
        /// </summary>
        /// <param name="password">The new password in clear text.</param>
        /// <returns>The salted hash.</returns>
        /// <exception cref="DomainException">
        /// Thrown when the password is shorter than 12 characters or lacks
        /// an uppercase letter, a lowercase letter, a digit, or a special
        /// character. The reason is operator-safe: it never echoes the
        /// password.
        /// </exception>
        public static PasswordHash Create(string? password)
        {
            ValidateCriteria(password);
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password!, salt, DefaultIterations);
            return new PasswordHash(salt, hash, DefaultIterations);
        }

        /// <summary>
        /// Rehydrates a stored hash, e.g. one loaded from storage.
        /// </summary>
        /// <param name="salt">The stored salt. Must be 16 bytes.</param>
        /// <param name="hash">The stored derived key. Must be 32 bytes.</param>
        /// <param name="iterations">The stored iteration count. Must be positive.</param>
        /// <returns>The rehydrated hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="salt"/> or <paramref name="hash"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when a size or count is invalid.</exception>
        public static PasswordHash Restore(byte[] salt, byte[] hash, int iterations)
        {
            ArgumentNullException.ThrowIfNull(salt);
            ArgumentNullException.ThrowIfNull(hash);
            if (salt.Length != SaltBytes || hash.Length != HashBytes || iterations < 1)
            {
                throw new DomainException("A stored password hash has invalid size or iterations.");
            }

            return new PasswordHash((byte[])salt.Clone(), (byte[])hash.Clone(), iterations);
        }

        /// <summary>
        /// Checks a clear-text candidate in constant time.
        /// </summary>
        /// <param name="password">The candidate. Null or empty never matches.</param>
        /// <returns>True when the candidate derives to the stored hash.</returns>
        public bool Verify(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            var candidate = Derive(password, _salt, Iterations);
            return CryptographicOperations.FixedTimeEquals(candidate, _hash);
        }

        /// <summary>
        /// Compares hashes by iterations, salt, and derived key.
        /// </summary>
        /// <param name="other">The other hash, if any.</param>
        /// <returns>True when both describe the same derived key.</returns>
        public bool Equals(PasswordHash? other)
        {
            if (other is null)
            {
                return false;
            }

            return Iterations == other.Iterations
                && CryptographicOperations.FixedTimeEquals(_salt, other._salt)
                && CryptographicOperations.FixedTimeEquals(_hash, other._hash);
        }

        /// <summary>
        /// Compares hashes by value.
        /// </summary>
        /// <param name="obj">The other object, if any.</param>
        /// <returns>True when <paramref name="obj"/> is an equal hash.</returns>
        public override bool Equals(object? obj)
        {
            return Equals(obj as PasswordHash);
        }

        /// <summary>
        /// Gets a hash code consistent with <see cref="Equals(PasswordHash)"/>.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Iterations,
                Convert.ToBase64String(_salt),
                Convert.ToBase64String(_hash)
            );
        }

        /// <summary>
        /// Derives a key with PBKDF2-SHA256.
        /// </summary>
        /// <param name="password">The clear-text password.</param>
        /// <param name="salt">The salt.</param>
        /// <param name="iterations">The iteration count.</param>
        /// <returns>The derived key.</returns>
        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashBytes
            );
        }

        /// <summary>
        /// Enforces the password criteria with operator-safe reasons.
        /// </summary>
        /// <param name="password">The candidate in clear text.</param>
        /// <exception cref="DomainException">Thrown when any criterion fails.</exception>
        private static void ValidateCriteria(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12)
            {
                throw new DomainException("Password must be at least 12 characters.");
            }

            var upper = false;
            var lower = false;
            var digit = false;
            var special = false;
            foreach (var c in password)
            {
                upper |= char.IsUpper(c);
                lower |= char.IsLower(c);
                digit |= char.IsDigit(c);
                special |= !char.IsLetterOrDigit(c);
            }

            if (!upper)
            {
                throw new DomainException("Password must contain an uppercase letter.");
            }

            if (!lower)
            {
                throw new DomainException("Password must contain a lowercase letter.");
            }

            if (!digit)
            {
                throw new DomainException("Password must contain a digit.");
            }

            if (!special)
            {
                throw new DomainException("Password must contain a special character.");
            }
        }
    }
}
