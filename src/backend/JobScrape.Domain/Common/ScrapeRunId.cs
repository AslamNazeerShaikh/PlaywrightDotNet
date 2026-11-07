namespace JobScrape.Domain.Common
{
    /// <summary>
    /// Strongly typed identifier of a <c>ScrapeRun</c>. Prevents swapping
    /// profile and run ids in signatures.
    /// </summary>
    public readonly record struct ScrapeRunId
    {
        /// <summary>
        /// Initializes a new instance. Private so ids are created through
        /// <see cref="New"/> or the validating <see cref="From"/>.
        /// </summary>
        /// <param name="value">The underlying identifier.</param>
        private ScrapeRunId(Guid value)
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
        public static ScrapeRunId New()
        {
            return new ScrapeRunId(Guid.CreateVersion7());
        }

        /// <summary>
        /// Wraps an existing identifier, e.g. one loaded from storage.
        /// </summary>
        /// <param name="value">The identifier. Must not be empty.</param>
        /// <returns>The typed identifier.</returns>
        /// <exception cref="DomainException">Thrown when <paramref name="value"/> is empty.</exception>
        public static ScrapeRunId From(Guid value)
        {
            return value == Guid.Empty
                ? throw new DomainException("A scrape run id cannot be empty.")
                : new ScrapeRunId(value);
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
