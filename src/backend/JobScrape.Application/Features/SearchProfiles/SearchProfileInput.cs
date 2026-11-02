namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// One search-profile field set from a create request or an update
    /// request. The update handler fills the immutable source from the stored
    /// profile. Nullable fields let the validator report each bad field by
    /// its JSON name.
    /// </summary>
    /// <param name="Name">Unique profile name.</param>
    /// <param name="SourceKey">Source key: <c>naukri</c>, <c>indeed</c>, or <c>linkedin</c>.</param>
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
    public sealed record SearchProfileInput(
        string? Name,
        string? SourceKey,
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
        IReadOnlyList<string>? RequiredSkills
    );
}
