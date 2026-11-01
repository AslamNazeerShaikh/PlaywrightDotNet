namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// Whether runs against the source can pass the compliance gate.
    /// </summary>
    /// <param name="Allowed">True when the configured consent satisfies the tier.</param>
    /// <param name="Reason">Operator guidance naming what is missing; null when allowed.</param>
    public sealed record SourceGate(bool Allowed, string? Reason);
}
