namespace JobScrape.Application.Results
{
    /// <summary>
    /// A typed, expected operational failure. <c>Code</c> is the stable
    /// machine-readable contract (the cockpit maps codes, never prose);
    /// <c>Title</c> and <c>Detail</c> are human-readable and safe to log.
    /// Never carries cookies, HTML, tokens, or PII in <c>Metadata</c>.
    /// </summary>
    public sealed record ErrorInfo(
        string Code,
        string Title,
        string Detail,
        ErrorKind Kind,
        IReadOnlyDictionary<string, object?>? Metadata = null);
}
