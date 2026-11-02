namespace JobScrape.Domain.Abstractions
{
    /// <summary>
    /// Base of every domain event: something meaningful that happened to an
    /// aggregate. Events carry ids and counts only, never cookies or PII, and
    /// are dispatched in-process after the unit of work saves, so handlers
    /// never observe uncommitted state (docs/03-domain-model.md §7).
    /// </summary>
    /// <param name="OccurredAt">When the transition happened, from the injected clock.</param>
    public abstract record DomainEvent(DateTimeOffset OccurredAt);
}
