using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Identifies one result page by its first card and its result-range
    /// text, so pagination can detect that it was served the same page twice
    /// and stop instead of silently re-reading it (docs/02-architecture.md §9.1).
    /// </summary>
    public sealed record PageFingerprint
    {
        /// <summary>
        /// Initializes a new instance; at least one component must be present.
        /// </summary>
        /// <param name="firstExternalId">External id of the first card, if any.</param>
        /// <param name="resultRange">Result-range text such as <c>1 - 20 of 431</c>, if shown.</param>
        /// <exception cref="DomainException">Thrown when both components are blank.</exception>
        public PageFingerprint(string? firstExternalId, string? resultRange)
        {
            FirstExternalId = Text.CollapseOrNull(firstExternalId);
            ResultRange = Text.CollapseOrNull(resultRange);
            if (FirstExternalId is null && ResultRange is null)
            {
                throw new DomainException("A page fingerprint needs a first id or a result range.");
            }
        }

        /// <summary>
        /// Gets the external id of the first card, if known.
        /// </summary>
        public string? FirstExternalId { get; }

        /// <summary>
        /// Gets the normalized result-range text, if known.
        /// </summary>
        public string? ResultRange { get; }
    }
}
