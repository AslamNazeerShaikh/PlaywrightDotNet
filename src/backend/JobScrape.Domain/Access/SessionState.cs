namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Health of a browser profile's session (docs/03-domain-model.md §6.2).
    /// Values are stored and never repurposed.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Never verified, or reset.
        /// </summary>
        New,

        /// <summary>
        /// Verified by a successful probe.
        /// </summary>
        Healthy,

        /// <summary>
        /// A wall was detected. Reserved for the transient observation;
        /// <see cref="BrowserProfile.MarkChallenged"/> records the stop as
        /// <see cref="CoolingDown"/> directly.
        /// </summary>
        Challenged,

        /// <summary>
        /// Stopped after a wall; launches are blocked until the cooldown ends.
        /// </summary>
        CoolingDown,

        /// <summary>
        /// Cookies or session expired; needs a re-login.
        /// </summary>
        Expired,

        /// <summary>
        /// Too many consecutive walls; the profile must be reset or replaced.
        /// </summary>
        ResetRequired,
    }
}
