namespace JobScrape.Application.Results
{
    /// <summary>
    /// Stable machine-readable failure categories. The API edge maps these to
    /// HTTP status codes; the CLI maps them to exit codes. Never serialized
    /// with HTTP semantics attached.
    /// </summary>
    public enum ErrorKind
    {
        /// <summary>
        /// The request failed validation. Maps to HTTP 400 and exit code 2.
        /// </summary>
        Validation,

        /// <summary>
        /// Authentication failed or is missing. Maps to HTTP 401.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// A referenced profile, run, or posting does not exist. Maps to HTTP 404.
        /// </summary>
        NotFound,

        /// <summary>
        /// The request conflicts with current state, e.g. a run is already
        /// active. Maps to HTTP 409.
        /// </summary>
        Conflict,

        /// <summary>
        /// A compliance gate blocked the operation. Maps to HTTP 403.
        /// </summary>
        Forbidden,

        /// <summary>
        /// The source rate-limited the caller. Maps to HTTP 429.
        /// </summary>
        RateLimited,

        /// <summary>
        /// A daily page, detail, or run quota is exhausted. Maps to HTTP 429.
        /// </summary>
        QuotaExceeded,

        /// <summary>
        /// The source presented a challenge or auth wall. The run pauses for a
        /// human instead of retrying. Maps to HTTP 409.
        /// </summary>
        Challenge,

        /// <summary>
        /// The source is in a cooldown period after a wall. Maps to HTTP 423.
        /// </summary>
        CoolingDown,

        /// <summary>
        /// The source changed its markup or flow in a way the parser cannot
        /// handle. Maps to HTTP 502.
        /// </summary>
        SourceChanged,

        /// <summary>
        /// An upstream dependency is temporarily unavailable. Maps to HTTP 503.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Local disk space is below the safety threshold. Maps to HTTP 507.
        /// </summary>
        Storage,

        /// <summary>
        /// An unexpected failure with no safer category. Maps to HTTP 500.
        /// </summary>
        Unexpected,
    }
}
