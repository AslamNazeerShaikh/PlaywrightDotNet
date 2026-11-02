using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The per-tier consent gate (docs/03-domain-model.md §8.6,
    /// docs/07-anti-bot-and-compliance.md §13). Pure; a config flag alone
    /// never unlocks a high-risk source, an interactive confirmation is also
    /// required.
    /// </summary>
    public static class CompliancePolicy
    {
        /// <summary>
        /// Whether a run against <paramref name="source"/> may start.
        /// </summary>
        /// <param name="source">The source. Must not be null.</param>
        /// <param name="consent">The operator's acknowledgments. Must not be null.</param>
        /// <returns>Allowed, or blocked with guidance naming what is missing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static ComplianceDecision CanRun(SourceKey source, ComplianceConsent consent)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(consent);

            return source.Tier switch
            {
                ComplianceTier.Standard => new ComplianceDecision(true, null),
                ComplianceTier.Restricted when consent.AcknowledgeSourceRisk =>
                    new ComplianceDecision(true, null),
                ComplianceTier.Restricted => new ComplianceDecision(
                    false,
                    $"Source '{source}' is restricted: set AcknowledgeSourceRisk=true to opt in."
                ),
                _ when consent.AcknowledgeSourceRisk
                        && consent.AllowHighRiskSources
                        && consent.InteractiveConfirmation => new ComplianceDecision(true, null),
                _ => new ComplianceDecision(
                    false,
                    $"Source '{source}' is high-risk: requires AcknowledgeSourceRisk=true, "
                        + "AllowHighRiskSources=true, and interactive confirmation."
                ),
            };
        }
    }
}
