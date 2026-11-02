namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Classification of what a source returned instead of (or alongside)
    /// results. Drives the response policy: only <see cref="TransientError"/>
    /// is ever retried. Values are never repurposed.
    /// </summary>
    public enum ChallengeKind
    {
        /// <summary>
        /// No challenge: the page is usable.
        /// </summary>
        None,

        /// <summary>
        /// HTTP 429/999 or an explicit throttle; wait for the retry window.
        /// </summary>
        RateLimited,

        /// <summary>
        /// A JS or interstitial check that a human can pass.
        /// </summary>
        SoftChallenge,

        /// <summary>
        /// A CAPTCHA. Never solved automatically.
        /// </summary>
        Captcha,

        /// <summary>
        /// A login or auth wall; the session must be bootstrapped manually.
        /// </summary>
        LoginRequired,

        /// <summary>
        /// The source denied access (e.g. 403 block page).
        /// </summary>
        AccessDenied,

        /// <summary>
        /// The page structure changed so results cannot be parsed.
        /// </summary>
        SourceChanged,

        /// <summary>
        /// A timeout, reset, or 5xx that may succeed on a bounded retry.
        /// </summary>
        TransientError,
    }
}
