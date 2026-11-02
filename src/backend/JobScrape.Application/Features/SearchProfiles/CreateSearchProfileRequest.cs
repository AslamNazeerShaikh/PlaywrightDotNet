namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// A request to create a search profile. Every field is nullable so the
    /// handler can report each missing or invalid field by its JSON name.
    /// Omitted caps default to the source's policy limits.
    /// </summary>
    /// <param name="Name">Unique profile name.</param>
    /// <param name="SourceKey">Source key: <c>naukri</c>, <c>indeed</c>, or <c>linkedin</c>.</param>
    /// <param name="Keywords">Search keywords as the source query expects them.</param>
    /// <param name="Locations">Cities or regions; may be empty when remote-only.</param>
    /// <param name="ExperienceYears">Optional years of experience, 0–50.</param>
    /// <param name="PostedWithinDays">Optional recency window, 1–30 days.</param>
    /// <param name="RemoteOnly">True to search remote roles only.</param>
    /// <param name="MaxPages">Optional page cap, up to the source default.</param>
    /// <param name="MaxJobs">Optional job cap, up to the source default.</param>
    /// <param name="MaxDetails">Optional detail cap, up to the source default.</param>
    /// <param name="MinDelayMs">Optional shortest delay, at least the source default.</param>
    /// <param name="MaxDelayMs">Optional longest delay, at least the source default and at most 120 000.</param>
    /// <param name="RequiredSkills">Optional key skills; a posting is kept when any one matches.</param>
    public sealed record CreateSearchProfileRequest(
        string? Name,
        string? SourceKey,
        string? Keywords,
        IReadOnlyList<string>? Locations,
        int? ExperienceYears,
        int? PostedWithinDays,
        bool RemoteOnly,
        int? MaxPages = null,
        int? MaxJobs = null,
        int? MaxDetails = null,
        int? MinDelayMs = null,
        int? MaxDelayMs = null,
        IReadOnlyList<string>? RequiredSkills = null
    );
}
