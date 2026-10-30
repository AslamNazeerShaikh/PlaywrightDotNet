namespace JobScrape.Application.Features.Health
{
    /// <summary>
    /// Liveness payload. A liveness check has no expected failure modes, so
    /// the handler returns this DTO directly instead of a <c>Result</c>.
    /// </summary>
    public sealed record GetHealthResponse(string Status, DateTimeOffset TimeUtc);
}
