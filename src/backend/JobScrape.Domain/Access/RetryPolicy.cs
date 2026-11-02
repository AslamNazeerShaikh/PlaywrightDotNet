using JobScrape.Domain.Abstractions;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Bounded retry for browser navigation (docs/03-domain-model.md §8.2,
    /// docs/02-architecture.md §12.2). Only <see cref="ChallengeKind.TransientError"/>
    /// is ever retried; walls, rate limits, and changed pages never are,
    /// because a retry storm is how a soft block becomes a ban.
    /// </summary>
    public sealed class RetryPolicy
    {
        /// <summary>
        /// Hard ceiling on total attempts, whatever the configuration says.
        /// </summary>
        public const int MaxAllowedAttempts = 3;

        /// <summary>
        /// Backoff before the first retry; each later retry waits 4x longer.
        /// </summary>
        public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Upper bound of the random jitter added to each backoff.
        /// </summary>
        public static readonly TimeSpan MaxJitter = TimeSpan.FromSeconds(1);

        // Both injected and never mutated.
        private readonly int _maxAttempts;
        private readonly IRandomSource _random;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="maxAttempts">Total attempts including the first, 1–<see cref="MaxAllowedAttempts"/>.</param>
        /// <param name="random">The jitter source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when <paramref name="maxAttempts"/> is out of range.</exception>
        public RetryPolicy(int maxAttempts, IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(random);
            if (maxAttempts is < 1 or > MaxAllowedAttempts)
            {
                throw new DomainException($"maxAttempts must be 1..{MaxAllowedAttempts}.");
            }

            _maxAttempts = maxAttempts;
            _random = random;
        }

        /// <summary>
        /// Whether a failed attempt may be retried.
        /// </summary>
        /// <param name="kind">How the attempt failed.</param>
        /// <param name="attempt">The 1-based attempt that just failed.</param>
        /// <returns>True only for a transient error with attempts left.</returns>
        /// <exception cref="DomainException">Thrown when <paramref name="attempt"/> is below 1.</exception>
        public bool ShouldRetry(ChallengeKind kind, int attempt)
        {
            if (attempt < 1)
            {
                throw new DomainException("attempt is 1-based.");
            }

            return kind == ChallengeKind.TransientError && attempt < _maxAttempts;
        }

        /// <summary>
        /// The wait before retrying after <paramref name="attempt"/> failed:
        /// exponential (2s, 8s, ...) plus jitter, and never less than a
        /// source-stated <c>Retry-After</c>.
        /// </summary>
        /// <param name="attempt">The 1-based attempt that just failed.</param>
        /// <param name="retryAfter">The source-stated delay, if any.</param>
        /// <returns>The delay before the next attempt.</returns>
        /// <exception cref="DomainException">Thrown when <paramref name="attempt"/> is below 1.</exception>
        public TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
        {
            if (attempt < 1)
            {
                throw new DomainException("attempt is 1-based.");
            }

            var exponential = BaseDelay * Math.Pow(4, Math.Min(attempt, MaxAllowedAttempts) - 1);
            var jitterMs = Math.Clamp(
                _random.NextInt32(0, (int)MaxJitter.TotalMilliseconds + 1),
                0,
                (int)MaxJitter.TotalMilliseconds
            );
            var backoff = exponential + TimeSpan.FromMilliseconds(jitterMs);
            return retryAfter > backoff ? retryAfter.Value : backoff;
        }
    }
}
