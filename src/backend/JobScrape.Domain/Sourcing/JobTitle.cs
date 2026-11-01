using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A posting title: whitespace-collapsed, 1–300 characters, no control
    /// characters. A posting without a title is dropped by the adapter.
    /// </summary>
    public sealed record JobTitle
    {
        /// <summary>
        /// Maximum title length.
        /// </summary>
        public const int MaxLength = 300;

        /// <summary>
        /// Initializes a new instance with an already-validated value.
        /// </summary>
        /// <param name="value">The normalized title.</param>
        private JobTitle(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the normalized title.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates a title.
        /// </summary>
        /// <param name="value">The raw title.</param>
        /// <returns>The title.</returns>
        /// <exception cref="DomainException">Thrown when the title is blank, too long, or has control characters.</exception>
        public static JobTitle Create(string? value)
        {
            var normalized = Text.Collapse(value);
            if (normalized.Length is 0 or > MaxLength)
            {
                throw new DomainException($"Job title must be 1..{MaxLength} characters.");
            }

            if (Text.HasControlCharacters(normalized))
            {
                throw new DomainException("Job title cannot contain control characters.");
            }

            return new JobTitle(normalized);
        }

        /// <summary>
        /// Returns the title.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
