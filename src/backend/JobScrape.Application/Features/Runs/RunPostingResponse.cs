namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// A posting a run saw, as stored after the run. Missing source fields
    /// stay null.
    /// </summary>
    /// <param name="Id">The posting identifier.</param>
    /// <param name="ExternalJobId">The source's own job id.</param>
    /// <param name="Url">The canonical posting URL.</param>
    /// <param name="Title">The job title.</param>
    /// <param name="Company">The employer, when listed.</param>
    /// <param name="Location">The raw location text, when listed.</param>
    /// <param name="WorkMode"><c>Onsite</c>, <c>Hybrid</c>, <c>Remote</c>, or <c>Unknown</c>.</param>
    /// <param name="Experience">The raw experience text, when listed.</param>
    /// <param name="Salary">The raw salary text, when listed.</param>
    /// <param name="PostedAt">When the source says it was posted, when known.</param>
    /// <param name="Skills">The listed skills.</param>
    /// <param name="Enriched">Whether a detail page filled in the description.</param>
    /// <param name="IsNew">Whether this run stored it for the first time.</param>
    /// <param name="FirstSeenAt">When any run first saw it.</param>
    /// <param name="LastSeenAt">When a run last saw it.</param>
    public sealed record RunPostingResponse(
        Guid Id,
        string ExternalJobId,
        string Url,
        string Title,
        string? Company,
        string? Location,
        string WorkMode,
        string? Experience,
        string? Salary,
        DateTimeOffset? PostedAt,
        IReadOnlyList<string> Skills,
        bool Enriched,
        bool IsNew,
        DateTimeOffset FirstSeenAt,
        DateTimeOffset LastSeenAt
    );
}
