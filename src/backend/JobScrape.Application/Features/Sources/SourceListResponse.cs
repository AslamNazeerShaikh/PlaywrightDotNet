namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// Every configured source, ordered by key.
    /// </summary>
    /// <param name="Items">The sources.</param>
    /// <param name="AsOf">When the snapshot was taken (UTC).</param>
    public sealed record SourceListResponse(
        IReadOnlyList<SourceStatusResponse> Items,
        DateTimeOffset AsOf
    );
}
