namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// A full search-profile replacement. The source is immutable and is not
    /// part of this request. The edit form sends every field, so a null
    /// optional filter clears it.
    /// </summary>
    /// <param name="Name">Unique profile name.</param>
    /// <param name="Keywords">Search keywords as the source query expects them.</param>
    /// <param name="Locations">Cities or regions; may be empty when remote-only.</param>
    /// <param name="ExperienceYears">Optional years of experience, 0–50.</param>
    /// <param name="PostedWithinDays">Optional recency window, 1–30 days.</param>
    /// <param name="RemoteOnly">True to search remote roles only.</param>
    /// <param name="MaxPages">Page cap, up to the source default.</param>
    /// <param name="MaxJobs">Job cap, up to the source default.</param>
    /// <param name="MaxDetails">Detail cap, up to the source default.</param>
    /// <param name="MinDelayMs">Shortest delay, at least the source default.</param>
    /// <param name="MaxDelayMs">Longest delay, at least the source default and within the hard cap.</param>
    /// <param name="RequiredSkills">Optional key skills; a posting is kept when any one matches.</param>
    /// <param name="Companies">Optional company names for the include and exclude exports.</param>
    /// <param name="Enabled">Whether new runs may start.</param>
    public sealed record UpdateSearchProfileRequest(
        string? Name,
        string? Keywords,
        IReadOnlyList<string>? Locations,
        int? ExperienceYears,
        int? PostedWithinDays,
        bool RemoteOnly,
        int? MaxPages,
        int? MaxJobs,
        int? MaxDetails,
        int? MinDelayMs,
        int? MaxDelayMs,
        IReadOnlyList<string>? RequiredSkills,
        IReadOnlyList<string>? Companies,
        bool Enabled
    );
}
