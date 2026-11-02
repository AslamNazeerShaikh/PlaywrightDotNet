namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The operator's risk acknowledgments for a run
    /// (docs/07-anti-bot-and-compliance.md §13).
    /// </summary>
    /// <param name="AcknowledgeSourceRisk">Config flag accepting the risk of restricted and high-risk sources.</param>
    /// <param name="AllowHighRiskSources">Config flag allowing high-risk sources at all.</param>
    /// <param name="InteractiveConfirmation">The operator confirmed interactively for this run.</param>
    public sealed record ComplianceConsent(
        bool AcknowledgeSourceRisk,
        bool AllowHighRiskSources,
        bool InteractiveConfirmation
    )
    {
        /// <summary>
        /// No acknowledgments: only standard sources may run.
        /// </summary>
        public static ComplianceConsent None { get; } = new(false, false, false);
    }
}
