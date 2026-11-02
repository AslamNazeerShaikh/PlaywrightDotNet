namespace JobScrape.Domain.Access
{
    /// <summary>
    /// State of a source's circuit breaker (docs/03-domain-model.md §6.3).
    /// Values are stored and never repurposed.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Runs may start normally.
        /// </summary>
        Closed,

        /// <summary>
        /// Runs are blocked until the cooldown ends.
        /// </summary>
        Open,

        /// <summary>
        /// One probe run (one page, no enrichment) may test the source.
        /// </summary>
        HalfOpen,
    }
}
