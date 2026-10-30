namespace JobScrape.Application.Results
{
    // CA1716: the name Error is the documented application-pattern contract
    // (docs/14-application-patterns.md section 3). Kept intentionally.
#pragma warning disable CA1716
    /// <summary>
    /// A typed, expected operational failure. <c>Code</c> is the stable
    /// machine-readable contract (the cockpit maps codes, never prose);
    /// <c>Title</c> and <c>Detail</c> are human-readable and safe to log.
    /// Never carries cookies, HTML, tokens, or PII in <c>Metadata</c>.
    /// </summary>
    public sealed record Error(
        string Code,
        string Title,
        string Detail,
        ErrorKind Kind,
        IReadOnlyDictionary<string, object?>? Metadata = null);
}
