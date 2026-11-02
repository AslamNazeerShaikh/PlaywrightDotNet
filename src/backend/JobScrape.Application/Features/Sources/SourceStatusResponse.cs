namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// One source as the cockpit's Sources page shows it: policy, consent
    /// gate, today's quota usage, and circuit state. Delays are
    /// milliseconds; timestamps UTC.
    /// </summary>
    /// <param name="Key">Lowercase source key.</param>
    /// <param name="Tier">Compliance tier: <c>Standard</c>, <c>Restricted</c>, or <c>HighRisk</c>.</param>
    /// <param name="Enabled">Whether configuration enables the source.</param>
    /// <param name="Gate">The compliance gate under the configured consent flags.</param>
    /// <param name="Policy">Default caps and pacing for new profiles.</param>
    /// <param name="Quota">Today's usage against the daily caps.</param>
    /// <param name="Circuit">The source's circuit breaker.</param>
    public sealed record SourceStatusResponse(
        string Key,
        string Tier,
        bool Enabled,
        SourceGate Gate,
        SourcePolicy Policy,
        SourceQuotaUsage Quota,
        SourceCircuit Circuit
    );
}
