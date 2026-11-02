namespace JobScrape.Domain.Common
{
    /// <summary>
    /// Risk tier of a job source. Drives the consent gate a run must pass
    /// before it may start (docs/07-anti-bot-and-compliance.md §13). Values
    /// are never repurposed; a new tier gets a new member.
    /// </summary>
    public enum ComplianceTier
    {
        /// <summary>
        /// Normal configuration and caps are enough. Enabled by default.
        /// </summary>
        Standard,

        /// <summary>
        /// Disabled by default; requires an explicit source-risk
        /// acknowledgment and very low caps.
        /// </summary>
        Restricted,

        /// <summary>
        /// Disabled by default; requires the source-risk acknowledgment, the
        /// high-risk config flag, and an interactive confirmation.
        /// </summary>
        HighRisk,
    }
}
