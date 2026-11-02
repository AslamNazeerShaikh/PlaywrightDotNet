using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A skill tag owned by a posting. <see cref="NormalizedName"/> is the
    /// case-insensitive dedupe and aggregation key; <see cref="Name"/> keeps
    /// the display casing.
    /// </summary>
    public sealed record JobSkill
    {
        /// <summary>
        /// Initializes a new instance with already-validated values.
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <param name="normalizedName">The lower-case key.</param>
        private JobSkill(string name, string normalizedName)
        {
            Name = name;
            NormalizedName = normalizedName;
        }

        /// <summary>
        /// Gets the display name, e.g. <c>ASP.NET Core</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the lower-case key, e.g. <c>asp.net core</c>.
        /// </summary>
        public string NormalizedName { get; }

        /// <summary>
        /// Creates a skill from raw text.
        /// </summary>
        /// <param name="name">The raw skill. Whitespace-collapsed; must be non-blank with no control characters.</param>
        /// <returns>The skill.</returns>
        /// <exception cref="DomainException">Thrown when the name is blank or has control characters.</exception>
        public static JobSkill Create(string? name)
        {
            var normalized = Text.Collapse(name);
            if (normalized.Length == 0 || Text.HasControlCharacters(normalized))
            {
                throw new DomainException(
                    "A skill name must be non-blank with no control characters."
                );
            }

            return new JobSkill(normalized, normalized.ToLowerInvariant());
        }
    }
}
