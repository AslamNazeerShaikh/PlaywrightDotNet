using System.Security.Cryptography;
using System.Text;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Lowercase SHA-256 hex fingerprint of a posting's stable fields. Two
    /// sightings with equal hashes carry the same content; volatile fields
    /// (relative dates, applicant counts) are never hashed.
    /// </summary>
    public sealed record ContentHash
    {
        /// <summary>
        /// Initializes a new instance with an already-validated value.
        /// </summary>
        /// <param name="value">64 lowercase hex characters.</param>
        private ContentHash(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the 64-character lowercase hex digest.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Hashes an ordered list of fields. Each field is length-prefixed and
        /// null is encoded distinctly from empty, so no two different field
        /// lists can produce the same input text.
        /// </summary>
        /// <param name="fields">The ordered stable fields. Must not be null.</param>
        /// <returns>The hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is null.</exception>
        public static ContentHash Compute(IEnumerable<string?> fields)
        {
            ArgumentNullException.ThrowIfNull(fields);

            var builder = new StringBuilder();
            foreach (var field in fields)
            {
                if (field is null)
                {
                    builder.Append("-|");
                }
                else
                {
                    builder.Append(field.Length).Append(':').Append(field).Append('|');
                }
            }

            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return new ContentHash(Convert.ToHexStringLower(digest));
        }

        /// <summary>
        /// Wraps a stored digest, e.g. one loaded from the database.
        /// </summary>
        /// <param name="value">64 lowercase hex characters.</param>
        /// <returns>The hash.</returns>
        /// <exception cref="DomainException">Thrown when the value is not a lowercase SHA-256 hex digest.</exception>
        public static ContentHash Parse(string? value)
        {
            if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigitLower))
            {
                throw new DomainException("A content hash must be 64 lowercase hex characters.");
            }

            return new ContentHash(value);
        }

        /// <summary>
        /// Returns the hex digest.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
