using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// What <see cref="IndeedSearchParser"/> read from one Indeed results page.
    /// </summary>
    /// <param name="Jobs">
    /// The visible cards in page order, de-duplicated by job key.
    /// </param>
    /// <param name="ResultCount">
    /// The whitespace-collapsed result-count text, e.g. <c>1–10 of 200 jobs</c>,
    /// or null when the page shows none.
    /// </param>
    /// <param name="FirstJobKey">
    /// External id of the first emitted card, or null when no card was
    /// emitted. With <paramref name="ResultCount"/> it forms the page fingerprint.
    /// </param>
    /// <param name="HasNext">
    /// True when a visible, enabled pagination link to a later page exists.
    /// </param>
    /// <param name="DroppedCards">
    /// Visible cards that were dropped as malformed (no usable title, job
    /// link, or key). Hidden cards are skipped silently and not counted, so a
    /// rising value means selector drift.
    /// </param>
    public sealed record IndeedSearchPage(
        IReadOnlyList<JobSnapshot> Jobs,
        string? ResultCount,
        string? FirstJobKey,
        bool HasNext,
        int DroppedCards
    );
}
