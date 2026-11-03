namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// The postings a run saw, most recently seen first.
    /// </summary>
    /// <param name="RunId">The run.</param>
    /// <param name="Items">The postings.</param>
    /// <param name="Total">How many postings the run saw in total.</param>
    public sealed record RunPostingListResponse(
        Guid RunId,
        IReadOnlyList<RunPostingResponse> Items,
        int Total
    );
}
