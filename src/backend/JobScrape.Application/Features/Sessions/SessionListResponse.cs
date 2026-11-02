namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Every browser profile, ordered by name.
    /// </summary>
    /// <param name="Items">The profiles.</param>
    /// <param name="Total">The number of profiles.</param>
    public sealed record SessionListResponse(IReadOnlyList<SessionResponse> Items, int Total);
}
