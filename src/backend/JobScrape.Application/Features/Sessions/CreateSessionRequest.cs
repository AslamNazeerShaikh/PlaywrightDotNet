namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// A request to create a browser profile. Nullable fields let the
    /// handler report each problem by its JSON name.
    /// </summary>
    /// <param name="Name">Profile name: 1–64 of letters, digits, <c>.</c>, <c>_</c>, <c>-</c>; not starting with <c>.</c>.</param>
    /// <param name="SourceKey">The source the profile is used for.</param>
    /// <param name="Engine">Optional engine: <c>Chromium</c> (default), <c>Firefox</c>, or <c>WebKit</c>.</param>
    /// <param name="Channel">Optional channel: <c>Bundled</c> (default), <c>Chrome</c>, or <c>Edge</c>; branded channels need Chromium.</param>
    public sealed record CreateSessionRequest(
        string? Name,
        string? SourceKey,
        string? Engine = null,
        string? Channel = null
    );
}
