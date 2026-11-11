namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// A partial browser-profile update. A null field keeps its stored value.
    /// The source is immutable and is not part of this request.
    /// </summary>
    /// <param name="Name">New safe profile name, or null to keep it.</param>
    /// <param name="Engine">New engine, or null to keep it.</param>
    /// <param name="Channel">New channel: <c>Bundled</c>, or null to keep it.</param>
    public sealed record UpdateSessionRequest(string? Name, string? Engine, string? Channel);
}
