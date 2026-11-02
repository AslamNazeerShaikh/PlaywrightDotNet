using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A posting location: the raw text as the source showed it (which may
    /// list several cities) plus best-effort parsed parts. Parsed parts are
    /// optional; the raw text is never discarded.
    /// </summary>
    public sealed record JobLocation
    {
        /// <summary>
        /// Initializes a new instance, collapsing whitespace in every part.
        /// </summary>
        /// <param name="raw">The location text as shown. Must be non-blank.</param>
        /// <param name="city">Parsed city; blank means unknown.</param>
        /// <param name="region">Parsed state or region; blank means unknown.</param>
        /// <param name="country">Parsed country; blank means unknown.</param>
        /// <param name="workMode">Detected work mode.</param>
        /// <exception cref="DomainException">Thrown when <paramref name="raw"/> is blank or <paramref name="workMode"/> is undefined.</exception>
        public JobLocation(
            string? raw,
            string? city,
            string? region,
            string? country,
            WorkMode workMode
        )
        {
            var normalizedRaw = Text.Collapse(raw);
            if (normalizedRaw.Length == 0)
            {
                throw new DomainException("A location needs its raw text.");
            }

            if (!Enum.IsDefined(workMode))
            {
                throw new DomainException($"Unknown work mode {workMode}.");
            }

            Raw = normalizedRaw;
            City = Text.CollapseOrNull(city);
            Region = Text.CollapseOrNull(region);
            Country = Text.CollapseOrNull(country);
            WorkMode = workMode;
        }

        /// <summary>
        /// Gets the location text as shown by the source.
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// Gets the parsed city, if known.
        /// </summary>
        public string? City { get; }

        /// <summary>
        /// Gets the parsed state or region, if known.
        /// </summary>
        public string? Region { get; }

        /// <summary>
        /// Gets the parsed country, if known.
        /// </summary>
        public string? Country { get; }

        /// <summary>
        /// Gets the detected work mode.
        /// </summary>
        public WorkMode WorkMode { get; }
    }
}
