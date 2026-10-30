namespace JobScrape.Application.Results
{
    /// <summary>
    /// Stable machine-readable failure categories. The API edge maps these to
    /// HTTP status codes; the CLI maps them to exit codes. Never serialized
    /// with HTTP semantics attached.
    /// </summary>
    public enum ErrorKind
    {
        Validation,
        NotFound,
        Conflict,
        Forbidden,
        RateLimited,
        QuotaExceeded,
        Challenge,
        CoolingDown,
        SourceChanged,
        Unavailable,
        Storage,
        Unexpected,
    }
}
