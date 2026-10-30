namespace JobScrape.Application.Features.Health
{
    /// <summary>
    /// Liveness payload. A liveness check has no expected failure modes, so
    /// the handler returns this DTO directly instead of a <c>Result</c>.
    /// </summary>
    /// <param name="Status">
    /// The service status. Always <c>healthy</c> when this payload is produced;
    /// any failure to produce it surfaces as an HTTP 500, not a status value.
    /// </param>
    /// <param name="TimeUtc">
    /// The clock time at which the check ran, in UTC. Comes from an injected
    /// <see cref="TimeProvider"/> so tests can fix it.
    /// </param>
    public sealed record GetHealthResponse(string Status, DateTimeOffset TimeUtc);
}
