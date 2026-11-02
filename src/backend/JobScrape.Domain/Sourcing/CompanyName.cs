using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A hiring company's name: whitespace-collapsed, 1–200 characters, no
    /// control characters. Optional on a posting; absence sets a data-quality
    /// flag instead of dropping the posting.
    /// </summary>
    public sealed record CompanyName
    {
        /// <summary>
        /// Maximum name length.
        /// </summary>
        public const int MaxLength = 200;

        /// <summary>
        /// Initializes a new instance with an already-validated value.
        /// </summary>
        /// <param name="value">The normalized name.</param>
        private CompanyName(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the normalized name.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates a company name.
        /// </summary>
        /// <param name="value">The raw name.</param>
        /// <returns>The name.</returns>
        /// <exception cref="DomainException">Thrown when the name is blank, too long, or has control characters.</exception>
        public static CompanyName Create(string? value)
        {
            var normalized = Text.Collapse(value);
            if (normalized.Length is 0 or > MaxLength)
            {
                throw new DomainException($"Company name must be 1..{MaxLength} characters.");
            }

            if (Text.HasControlCharacters(normalized))
            {
                throw new DomainException("Company name cannot contain control characters.");
            }

            return new CompanyName(normalized);
        }

        /// <summary>
        /// Returns the name.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
