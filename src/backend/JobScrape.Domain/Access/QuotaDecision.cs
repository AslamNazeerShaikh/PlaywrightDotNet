namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Outcome of a quota check. When exceeded, nothing was consumed and the
    /// caller stops before the next page, detail, or run.
    /// </summary>
    /// <param name="IsAllowed">True when the unit was consumed.</param>
    /// <param name="Kind">The checked counter.</param>
    /// <param name="Limit">The daily cap that applied.</param>
    /// <param name="Used">The count after the check (unchanged when exceeded).</param>
    public sealed record QuotaDecision(bool IsAllowed, QuotaKind Kind, int Limit, int Used);
}
