namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The response to a classified page.
    /// </summary>
    /// <param name="Action">What the run must do.</param>
    /// <param name="Cooldown">How long to wait before resuming, for pauses; otherwise null.</param>
    /// <param name="Reason">Operator-safe reason, for stops; otherwise null.</param>
    public sealed record ChallengeDecision(
        ChallengeAction Action,
        TimeSpan? Cooldown = null,
        string? Reason = null
    );
}
