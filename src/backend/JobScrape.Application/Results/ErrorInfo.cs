namespace JobScrape.Application.Results
{
    /// <summary>
    /// A typed, expected operational failure. <c>Code</c> is the stable
    /// machine-readable contract (the cockpit maps codes, never prose);
    /// <c>Title</c> and <c>Detail</c> are human-readable and safe to log.
    /// Never carries cookies, HTML, tokens, or PII in <c>Metadata</c>.
    /// </summary>
    /// <param name="Code">
    /// Stable machine-readable code, e.g. <c>QUOTA_EXCEEDED</c>. Uppercase
    /// snake case; never localized and never repurposed once shipped.
    /// </param>
    /// <param name="Title">Short human-readable summary of the failure.</param>
    /// <param name="Detail">
    /// Operator-safe detail. Never contains stack traces, SQL, cookies, HTML,
    /// authorization headers, or filesystem paths.
    /// </param>
    /// <param name="Kind">
    /// The failure category. The API edge maps this to an HTTP status code;
    /// the CLI maps it to a process exit code.
    /// </param>
    /// <param name="Metadata">
    /// Optional IDs, counts, and retry hints. Never cookies, HTML, tokens,
    /// or PII.
    /// </param>
    public sealed record ErrorInfo(
        string Code,
        string Title,
        string Detail,
        ErrorKind Kind,
        IReadOnlyDictionary<string, object?>? Metadata = null
    );
}
