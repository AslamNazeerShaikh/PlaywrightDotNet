namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Read-model summary of a saved search profile. Profiles are the
    /// reusable "what to search" aggregate; this DTO carries only the fields
    /// the lookup endpoint exposes.
    /// </summary>
    /// <param name="Id">The stable profile identifier.</param>
    /// <param name="Name">Operator-given profile name.</param>
    /// <param name="SourceKey">Lowercase source key, e.g. <c>naukri</c>.</param>
    public sealed record SearchProfileResponse(Guid Id, string Name, string SourceKey);
}
