namespace JobScrape.Domain.Access
{
    /// <summary>
    /// What the run must do about a classified page. Values are never repurposed.
    /// </summary>
    public enum ChallengeAction
    {
        /// <summary>
        /// The page is usable; carry on.
        /// </summary>
        Continue,

        /// <summary>
        /// Retry the same request under the retry policy.
        /// </summary>
        Retry,

        /// <summary>
        /// Wait out a rate limit, then resume automatically.
        /// </summary>
        Pause,

        /// <summary>
        /// Stop and wait for a human to solve the wall; never auto-solved.
        /// </summary>
        ManualPause,

        /// <summary>
        /// Stop the run; the session must be bootstrapped again.
        /// </summary>
        Stop,

        /// <summary>
        /// Stop and open the source's circuit breaker.
        /// </summary>
        OpenCircuit,

        /// <summary>
        /// Stop and capture artifacts: the page structure changed.
        /// </summary>
        StopAndInspect,
    }
}
