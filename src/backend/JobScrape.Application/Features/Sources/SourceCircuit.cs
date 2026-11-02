namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// The source's circuit breaker; <c>Closed</c> with no failures when it never tripped.
    /// </summary>
    /// <param name="State"><c>Closed</c>, <c>Open</c>, or <c>HalfOpen</c>.</param>
    /// <param name="OpenUntil">When a probe may be attempted, while open.</param>
    /// <param name="CanProbe">True when open and the cooldown has elapsed.</param>
    /// <param name="ConsecutiveFailures">Walls in a row since the last success.</param>
    /// <param name="RecentFailures">Walls inside the 24-hour window.</param>
    public sealed record SourceCircuit(
        string State,
        DateTimeOffset? OpenUntil,
        bool CanProbe,
        int ConsecutiveFailures,
        int RecentFailures
    );
}
