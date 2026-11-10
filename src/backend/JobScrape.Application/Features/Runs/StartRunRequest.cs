namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Body of <c>POST /api/v1/runs</c>.
    /// </summary>
    /// <param name="ProfileId">The search profile to execute.</param>
    /// <param name="BrowserProfileId">The browser profile (session) to run in; bound to the profile's source.</param>
    /// <param name="Enrich">Whether to visit detail pages after searching, within the detail cap.</param>
    /// <param name="OnlyNew">Whether to run only postings not seen before; defaults to the full run.</param>
    public sealed record StartRunRequest(
        Guid ProfileId,
        Guid BrowserProfileId,
        bool Enrich = true,
        bool OnlyNew = false
    );
}
