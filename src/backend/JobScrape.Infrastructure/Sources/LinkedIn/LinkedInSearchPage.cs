using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// What <see cref="LinkedInSearchParser"/> read from one LinkedIn guest
    /// results page.
    /// </summary>
    /// <param name="Jobs">
    /// The visible cards in page order, de-duplicated by job id.
    /// </param>
    /// <param name="ResultCount">
    /// The whitespace-collapsed result-count text, or null when the page shows none.
    /// </param>
    /// <param name="FirstJobId">
    /// External id of the first emitted card, or null when no card was
    /// emitted. With <paramref name="ResultCount"/> it forms the page fingerprint.
    /// </param>
    /// <param name="HasNext">
    /// True when a visible pagination link to a later <c>start</c> offset exists.
    /// </param>
    /// <param name="DroppedCards">
    /// Visible cards that were dropped as malformed (no usable title, job
    /// link, or id). Hidden cards are skipped silently and not counted, so a
    /// rising value means selector drift.
    /// </param>
    public sealed record LinkedInSearchPage(
        IReadOnlyList<JobSnapshot> Jobs,
        string? ResultCount,
        string? FirstJobId,
        bool HasNext,
        int DroppedCards
    );
}
