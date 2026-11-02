namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// The most recent runs, newest first.
    /// </summary>
    /// <param name="Items">The runs.</param>
    /// <param name="Total">How many runs exist in all.</param>
    public sealed record RunListResponse(IReadOnlyList<RunResponse> Items, int Total);
}
