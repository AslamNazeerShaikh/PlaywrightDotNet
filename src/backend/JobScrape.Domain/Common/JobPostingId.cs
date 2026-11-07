namespace JobScrape.Domain.Common
{
    /// <summary>
    /// Strongly typed identifier of a <c>JobPosting</c>. Prevents swapping
    /// posting and profile ids in signatures.
    /// </summary>
    public readonly record struct JobPostingId
    {
        /// <summary>
        /// Initializes a new instance. Private so ids are created through
        /// <see cref="New"/> or the validating <see cref="From"/>.
        /// </summary>
        /// <param name="value">The underlying identifier.</param>
        private JobPostingId(Guid value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the underlying identifier.
        /// </summary>
        public Guid Value { get; }

        /// <summary>
        /// Creates a new unique identifier.
        /// </summary>
        /// <returns>A new, non-empty identifier.</returns>
        public static JobPostingId New()
        {
            return new JobPostingId(Guid.CreateVersion7());
        }

        /// <summary>
        /// Wraps an existing identifier, e.g. one loaded from storage.
        /// </summary>
        /// <param name="value">The identifier. Must not be empty.</param>
        /// <returns>The typed identifier.</returns>
        /// <exception cref="DomainException">Thrown when <paramref name="value"/> is empty.</exception>
        public static JobPostingId From(Guid value)
        {
            return value == Guid.Empty
                ? throw new DomainException("A job posting id cannot be empty.")
                : new JobPostingId(value);
        }

        /// <summary>
        /// Returns the underlying identifier in its default format.
        /// </summary>
        /// <returns>The identifier as a string.</returns>
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
