namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// Strongly typed identifier of a <c>Role</c>. Prevents swapping role
    /// and user ids in signatures.
    /// </summary>
    public readonly record struct RoleId
    {
        /// <summary>
        /// Initializes a new instance. Private so ids are created through
        /// <see cref="New"/> or the validating <see cref="From"/>.
        /// </summary>
        /// <param name="value">The underlying identifier.</param>
        private RoleId(Guid value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the underlying identifier.
        /// </summary>
        public Guid Value { get; }

        /// <summary>
        /// Creates a new time-ordered (UUIDv7) identifier. Ordered ids keep
        /// clustered index inserts sequential.
        /// </summary>
        /// <returns>A new, non-empty identifier.</returns>
        public static RoleId New()
        {
            return new RoleId(Guid.CreateVersion7());
        }

        /// <summary>
        /// Wraps an existing identifier, e.g. one loaded from storage.
        /// </summary>
        /// <param name="value">The identifier. Must not be empty.</param>
        /// <returns>The typed identifier.</returns>
        /// <exception cref="DomainException">Thrown when <paramref name="value"/> is empty.</exception>
        public static RoleId From(Guid value)
        {
            return value == Guid.Empty
                ? throw new DomainException("A role id cannot be empty.")
                : new RoleId(value);
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
