using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Raw-but-typed fields an adapter extracted from a search card or detail
    /// page, before domain validation. Optional fields left null (or an
    /// empty skill list) mean "not observed on this page": refreshing a
    /// posting keeps the stored value instead of erasing it.
    /// </summary>
    public sealed record JobSnapshot
    {
        /// <summary>
        /// Gets the source the page came from.
        /// </summary>
        public required SourceKey Source { get; init; }

        /// <summary>
        /// Gets the source's job id, extracted by the adapter.
        /// </summary>
        public required string ExternalJobId { get; init; }

        /// <summary>
        /// Gets the absolute job URL, before canonicalization.
        /// </summary>
        public required string Url { get; init; }

        /// <summary>
        /// Gets the raw title.
        /// </summary>
        public required string Title { get; init; }

        /// <summary>
        /// Gets the raw company name, if shown.
        /// </summary>
        public string? Company { get; init; }

        /// <summary>
        /// Gets the company page URL, if shown.
        /// </summary>
        public string? CompanyUrl { get; init; }

        /// <summary>
        /// Gets the location, if shown.
        /// </summary>
        public JobLocation? Location { get; init; }

        /// <summary>
        /// Gets the experience text as shown, e.g. <c>5-8 Yrs</c>.
        /// </summary>
        public string? ExperienceRaw { get; init; }

        /// <summary>
        /// Gets the parsed minimum years, when unambiguous.
        /// </summary>
        public int? ExperienceMinYears { get; init; }

        /// <summary>
        /// Gets the parsed maximum years, when unambiguous.
        /// </summary>
        public int? ExperienceMaxYears { get; init; }

        /// <summary>
        /// Gets the salary text as shown.
        /// </summary>
        public string? SalaryRaw { get; init; }

        /// <summary>
        /// Gets the parsed salary, when unambiguous.
        /// </summary>
        public SalaryRange? Salary { get; init; }

        /// <summary>
        /// Gets the posted-date text as shown, e.g. <c>3 days ago</c>.
        /// </summary>
        public string? PostedRaw { get; init; }

        /// <summary>
        /// Gets the parsed posted date (UTC), when known.
        /// </summary>
        public DateTimeOffset? PostedAt { get; init; }

        /// <summary>
        /// Gets the description, already stripped of markup and capped by the adapter.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the short card snippet.
        /// </summary>
        public string? Snippet { get; init; }

        /// <summary>
        /// Gets the raw skill tags; empty when none were shown.
        /// </summary>
        public IReadOnlyList<string> Skills { get; init; } = [];

        /// <summary>
        /// Gets the visible external apply URL, if any.
        /// </summary>
        public string? ApplyUrl { get; init; }

        /// <summary>
        /// Gets a value indicating whether the parser fell back to
        /// low-confidence selectors for this page.
        /// </summary>
        public bool LowConfidence { get; init; }
    }
}
