using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Read-model view of a saved search profile, as the API returns it.
    /// Delays are milliseconds and timestamps UTC.
    /// </summary>
    /// <param name="Id">The stable profile identifier.</param>
    /// <param name="Name">Operator-given profile name.</param>
    /// <param name="SourceKey">Lowercase source key, e.g. <c>naukri</c>.</param>
    /// <param name="Keywords">The normalized keyword string.</param>
    /// <param name="Locations">The distinct locations, in order.</param>
    /// <param name="ExperienceYears">Optional years-of-experience filter.</param>
    /// <param name="PostedWithinDays">Optional recency filter in days.</param>
    /// <param name="RemoteOnly">True when only remote roles are searched.</param>
    /// <param name="MaxPages">Result pages per run.</param>
    /// <param name="MaxJobs">Postings per run.</param>
    /// <param name="MaxDetails">Detail visits per run; 0 disables enrichment.</param>
    /// <param name="MinDelayMs">Shortest delay between requests.</param>
    /// <param name="MaxDelayMs">Longest delay between requests.</param>
    /// <param name="Enabled">Whether runs may start from this profile.</param>
    /// <param name="CreatedAt">When the profile was created.</param>
    /// <param name="UpdatedAt">When the profile last changed.</param>
    public sealed record SearchProfileResponse(
        Guid Id,
        string Name,
        string SourceKey,
        string Keywords,
        IReadOnlyList<string> Locations,
        int? ExperienceYears,
        int? PostedWithinDays,
        bool RemoteOnly,
        int MaxPages,
        int MaxJobs,
        int MaxDetails,
        int MinDelayMs,
        int MaxDelayMs,
        bool Enabled,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    )
    {
        /// <summary>
        /// Projects a profile aggregate to its read model.
        /// </summary>
        /// <param name="profile">The profile. Must not be null.</param>
        /// <returns>The read model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        public static SearchProfileResponse From(SearchProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return new SearchProfileResponse(
                profile.Id.Value,
                profile.Name,
                profile.Source.Value,
                profile.Criteria.Keywords,
                profile.Criteria.Locations,
                profile.Criteria.ExperienceYears,
                profile.Criteria.PostedWithinDays,
                profile.Criteria.RemoteOnly,
                profile.Limits.MaxPages,
                profile.Limits.MaxJobs,
                profile.Limits.MaxDetails,
                (int)profile.Limits.MinDelay.TotalMilliseconds,
                (int)profile.Limits.MaxDelay.TotalMilliseconds,
                profile.Enabled,
                profile.CreatedAt,
                profile.UpdatedAt
            );
        }
    }
}
