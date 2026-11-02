namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Outcome of the compliance gate.
    /// </summary>
    /// <param name="IsAllowed">True when the source may run.</param>
    /// <param name="Reason">Operator guidance when blocked; null when allowed.</param>
    public sealed record ComplianceDecision(bool IsAllowed, string? Reason);
}
