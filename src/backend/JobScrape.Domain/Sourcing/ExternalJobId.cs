using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A posting's id on its source (Naukri numeric suffix, LinkedIn view
    /// id, Indeed <c>jk</c>). Together with the source it is the posting's
    /// immutable identity. Source-specific formats are checked by the
    /// adapter; the domain enforces only the shape every id shares.
    /// </summary>
    public sealed record ExternalJobId
    {
        /// <summary>
        /// Maximum id length.
        /// </summary>
        public const int MaxLength = 128;

        /// <summary>
        /// Initializes a new instance with an already-validated value.
        /// </summary>
        /// <param name="value">The trimmed id.</param>
        private ExternalJobId(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the trimmed id.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates an id.
        /// </summary>
        /// <param name="value">The raw id. Trimmed; 1–128 characters with no whitespace or control characters.</param>
        /// <returns>The id.</returns>
        /// <exception cref="DomainException">Thrown when the id is blank, too long, or malformed.</exception>
        public static ExternalJobId Create(string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length is 0 or > MaxLength)
            {
                throw new DomainException($"External job id must be 1..{MaxLength} characters.");
            }

            if (trimmed.Any(char.IsWhiteSpace) || Text.HasControlCharacters(trimmed))
            {
                throw new DomainException(
                    "External job id cannot contain whitespace or control characters."
                );
            }

            return new ExternalJobId(trimmed);
        }

        /// <summary>
        /// Returns the id value.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
