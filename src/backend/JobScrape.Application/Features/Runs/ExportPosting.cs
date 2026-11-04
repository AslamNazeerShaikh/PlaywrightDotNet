namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// One posting as exported: the full stored fields, missing source
    /// fields staying null. Writers render every column; they never drop
    /// PII because the store holds none (descriptions may quote a posting's
    /// own contact line verbatim, which is source content, not operator PII).
    /// </summary>
    /// <param name="Id">The posting identifier.</param>
    /// <param name="ExternalJobId">The source's own job id.</param>
    /// <param name="Source">The source key, e.g. <c>naukri</c>.</param>
    /// <param name="Url">The canonical posting URL.</param>
    /// <param name="ApplyUrl">The visible external apply link, if any.</param>
    /// <param name="Title">The job title.</param>
    /// <param name="Company">The employer, when listed.</param>
    /// <param name="Location">The raw location text, when listed.</param>
    /// <param name="WorkMode"><c>Onsite</c>, <c>Hybrid</c>, <c>Remote</c>, or <c>Unknown</c>.</param>
    /// <param name="Experience">The raw experience text, when listed.</param>
    /// <param name="Salary">The raw salary text, when listed.</param>
    /// <param name="PostedAt">When the source says it was posted, when known.</param>
    /// <param name="Skills">The listed skills.</param>
    /// <param name="Description">The description, when enriched.</param>
    /// <param name="FirstSeenAt">When any run first saw it.</param>
    /// <param name="LastSeenAt">When a run last saw it.</param>
    public sealed record ExportPosting(
        Guid Id,
        string ExternalJobId,
        string Source,
        string Url,
        string? ApplyUrl,
        string Title,
        string? Company,
        string? Location,
        string WorkMode,
        string? Experience,
        string? Salary,
        DateTimeOffset? PostedAt,
        IReadOnlyList<string> Skills,
        string? Description,
        DateTimeOffset FirstSeenAt,
        DateTimeOffset LastSeenAt
    );
}
