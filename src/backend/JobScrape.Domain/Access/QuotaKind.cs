namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Which daily per-source counter a quota decision refers to. Values are
    /// never repurposed.
    /// </summary>
    public enum QuotaKind
    {
        /// <summary>
        /// Result pages fetched per day.
        /// </summary>
        Pages,

        /// <summary>
        /// Detail pages visited per day.
        /// </summary>
        Details,

        /// <summary>
        /// Runs started per day.
        /// </summary>
        Runs,
    }
}
