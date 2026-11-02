using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// What <see cref="NaukriSearchParser"/> read from one Naukri results page.
    /// </summary>
    /// <param name="Jobs">
    /// The visible organic cards in page order, de-duplicated by external id.
    /// Promoted widgets and honeypot/hidden cards are never included.
    /// </param>
    /// <param name="ResultRange">
    /// The whitespace-collapsed result-range text, e.g. <c>1 - 20 of 131</c>,
    /// or null when the page shows none.
    /// </param>
    /// <param name="FirstJobId">
    /// External id of the first emitted card, or null when no card was
    /// emitted. With <paramref name="ResultRange"/> it forms the page fingerprint.
    /// </param>
    /// <param name="HasNext">
    /// True when a visible native <b>Next</b> link is present and not
    /// <c>aria-disabled="true"</c>.
    /// </param>
    /// <param name="DroppedCards">
    /// Visible cards that were dropped as malformed (no usable title, job
    /// link, or id). Hidden/honeypot cards are skipped silently and not
    /// counted, so a rising value means selector drift.
    /// </param>
    public sealed record NaukriSearchPage(
        IReadOnlyList<JobSnapshot> Jobs,
        string? ResultRange,
        string? FirstJobId,
        bool HasNext,
        int DroppedCards
    );
}
