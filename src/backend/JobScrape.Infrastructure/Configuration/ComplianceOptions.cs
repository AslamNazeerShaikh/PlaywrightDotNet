namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Operator risk acknowledgments (docs/07-anti-bot-and-compliance.md §13).
    /// Both default to false; a config flag alone never unlocks a high-risk
    /// source, which also needs an interactive confirmation per run.
    /// </summary>
    public sealed class ComplianceOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Compliance";

        /// <summary>
        /// Gets a value indicating whether restricted and high-risk source risk is accepted.
        /// </summary>
        public bool AcknowledgeSourceRisk { get; init; }

        /// <summary>
        /// Gets a value indicating whether high-risk sources are allowed at all.
        /// </summary>
        public bool AllowHighRiskSources { get; init; }
    }
}
