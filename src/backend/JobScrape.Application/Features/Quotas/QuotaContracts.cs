namespace JobScrape.Application.Features.Quotas
{
    /// <summary>
    /// One usage quota definition for the admin surface: the governed
    /// scope plus its per-user limit and fixed window.
    /// </summary>
    /// <param name="Scope">The governed surface, e.g. <c>Api</c>.</param>
    /// <param name="Limit">Requests one user may make per window.</param>
    /// <param name="Window">The fixed window.</param>
    public sealed record QuotaResponse(string Scope, int Limit, TimeSpan Window);

    /// <summary>
    /// The quota definition list payload.
    /// </summary>
    /// <param name="Quotas">All definitions, in scope order.</param>
    public sealed record QuotaListResponse(IReadOnlyList<QuotaResponse> Quotas);
}
