namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Maps a page classification to the run's response
    /// (docs/03-domain-model.md §8.3). Pure: no I/O, no clock. Walls are
    /// never retried and never auto-solved.
    /// </summary>
    public static class ChallengePolicy
    {
        /// <summary>
        /// Cooldown for a rate limit with no stated <c>Retry-After</c>.
        /// </summary>
        public static readonly TimeSpan DefaultRateLimitCooldown = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Cooldown once walls repeat <see cref="BrowserProfile.MaxConsecutiveChallenges"/> times.
        /// </summary>
        public static readonly TimeSpan RepeatedChallengeCooldown = TimeSpan.FromDays(1);

        /// <summary>
        /// Decides the response to a classified page.
        /// </summary>
        /// <param name="assessment">The detector verdict. Must not be null.</param>
        /// <param name="consecutiveChallenges">Walls hit in a row so far, at least 0.</param>
        /// <param name="challengeCooldown">The source's configured wall cooldown; positive.</param>
        /// <returns>The decision.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assessment"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when a count or cooldown is out of range.</exception>
        public static ChallengeDecision Decide(
            ChallengeAssessment assessment,
            int consecutiveChallenges,
            TimeSpan challengeCooldown
        )
        {
            ArgumentNullException.ThrowIfNull(assessment);
            if (consecutiveChallenges < 0)
            {
                throw new DomainException("Consecutive challenges must be non-negative.");
            }

            if (challengeCooldown <= TimeSpan.Zero)
            {
                throw new DomainException("A challenge cooldown must be positive.");
            }

            return assessment.Kind switch
            {
                ChallengeKind.None => new ChallengeDecision(ChallengeAction.Continue),
                ChallengeKind.TransientError => new ChallengeDecision(ChallengeAction.Retry),
                ChallengeKind.RateLimited => new ChallengeDecision(
                    ChallengeAction.Pause,
                    assessment.RetryAfter ?? DefaultRateLimitCooldown
                ),
                ChallengeKind.SoftChallenge or ChallengeKind.Captcha => new ChallengeDecision(
                    ChallengeAction.ManualPause,
                    consecutiveChallenges >= BrowserProfile.MaxConsecutiveChallenges
                        ? RepeatedChallengeCooldown
                        : challengeCooldown
                ),
                ChallengeKind.LoginRequired => new ChallengeDecision(
                    ChallengeAction.Stop,
                    Reason: "Session login required."
                ),
                ChallengeKind.AccessDenied => new ChallengeDecision(
                    ChallengeAction.OpenCircuit,
                    Reason: "Access denied."
                ),
                // Assessments only carry defined kinds, so the remaining
                // case is SourceChanged.
                _ => new ChallengeDecision(
                    ChallengeAction.StopAndInspect,
                    Reason: "Page structure changed."
                ),
            };
        }
    }
}
