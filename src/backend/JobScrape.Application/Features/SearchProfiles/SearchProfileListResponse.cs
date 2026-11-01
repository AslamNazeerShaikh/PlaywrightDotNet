namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// The profile list, wrapped so paging fields can be added without
    /// breaking clients.
    /// </summary>
    /// <param name="Items">The profiles ordered by name.</param>
    /// <param name="Total">The number of profiles.</param>
    public sealed record SearchProfileListResponse(
        IReadOnlyList<SearchProfileResponse> Items,
        int Total
    );
}
