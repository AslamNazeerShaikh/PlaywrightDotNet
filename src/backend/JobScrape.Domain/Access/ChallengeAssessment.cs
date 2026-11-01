namespace JobScrape.Domain.Access
{
    /// <summary>
    /// A challenge detector's verdict on one page: what it is, how sure the
    /// detector is, and why. Adapters return this instead of throwing for a wall.
    /// </summary>
    public sealed record ChallengeAssessment
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="kind">The classification.</param>
        /// <param name="confidence">Detector confidence, 0–1.</param>
        /// <param name="evidence">
        /// Short operator-safe evidence, e.g. <c>status=403</c> or a matched
        /// title; never cookies or page HTML. Trimmed; may be empty for
        /// <see cref="ChallengeKind.None"/>.
        /// </param>
        /// <param name="retryAfter">Source-stated retry delay, if any; non-negative.</param>
        /// <exception cref="DomainException">Thrown when a value is out of range.</exception>
        public ChallengeAssessment(
            ChallengeKind kind,
            double confidence,
            string? evidence,
            TimeSpan? retryAfter = null
        )
        {
            if (!Enum.IsDefined(kind))
            {
                throw new DomainException($"Unknown challenge kind {kind}.");
            }

            if (double.IsNaN(confidence) || confidence is < 0 or > 1)
            {
                throw new DomainException("Confidence must be 0..1.");
            }

            if (retryAfter < TimeSpan.Zero)
            {
                throw new DomainException("retryAfter must be non-negative.");
            }

            Kind = kind;
            Confidence = confidence;
            Evidence = evidence?.Trim() ?? string.Empty;
            RetryAfter = retryAfter;
        }

        /// <summary>
        /// A clean page.
        /// </summary>
        public static ChallengeAssessment Clear { get; } = new(ChallengeKind.None, 1, null);

        /// <summary>
        /// Gets the classification.
        /// </summary>
        public ChallengeKind Kind { get; }

        /// <summary>
        /// Gets the detector confidence, 0–1.
        /// </summary>
        public double Confidence { get; }

        /// <summary>
        /// Gets the operator-safe evidence.
        /// </summary>
        public string Evidence { get; }

        /// <summary>
        /// Gets the source-stated retry delay, if any.
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        /// <summary>
        /// Gets a value indicating whether a human must act before the run
        /// can continue: walls that a person can solve or must log in past.
        /// </summary>
        public bool RequiresManualIntervention =>
            Kind
                is ChallengeKind.SoftChallenge
                    or ChallengeKind.Captcha
                    or ChallengeKind.LoginRequired;
    }
}
