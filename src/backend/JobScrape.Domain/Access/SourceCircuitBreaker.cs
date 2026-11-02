using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Aggregate root for one source's circuit breaker
    /// (docs/03-domain-model.md §6.3). Persisted per source, not per
    /// browser profile, so a fresh profile can never bypass a source-level
    /// block.
    /// <para>
    /// Trips immediately on <see cref="ChallengeKind.AccessDenied"/>
    /// (§8.3), and otherwise after <see cref="WindowThreshold"/> walls within
    /// <see cref="FailureWindow"/> or <see cref="ConsecutiveThreshold"/> walls
    /// in a row. A failed half-open probe re-opens it for
    /// <see cref="RepeatedBlockCooldown"/>.
    /// </para>
    /// </summary>
    public sealed class SourceCircuitBreaker : AggregateRoot
    {
        /// <summary>
        /// Walls within <see cref="FailureWindow"/> that open the circuit.
        /// </summary>
        public const int WindowThreshold = 3;

        /// <summary>
        /// Walls in a row, with no success between, that open the circuit.
        /// </summary>
        public const int ConsecutiveThreshold = 2;

        /// <summary>
        /// The sliding window for <see cref="WindowThreshold"/>.
        /// </summary>
        public static readonly TimeSpan FailureWindow = TimeSpan.FromHours(24);

        /// <summary>
        /// Cooldown when a rate limit opens the circuit.
        /// </summary>
        public static readonly TimeSpan RateLimitCooldown = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Cooldown when any other wall opens the circuit.
        /// </summary>
        public static readonly TimeSpan ChallengeCooldown = TimeSpan.FromHours(24);

        /// <summary>
        /// Cooldown when a half-open probe fails.
        /// </summary>
        public static readonly TimeSpan RepeatedBlockCooldown = TimeSpan.FromDays(7);

        // Wall times inside the window, oldest first; pruned on every write.
        private readonly List<DateTimeOffset> _recentFailures = [];

        /// <summary>
        /// Initializes a new closed breaker.
        /// </summary>
        /// <param name="source">The source.</param>
        private SourceCircuitBreaker(SourceKey source)
        {
            Source = source;
            State = CircuitState.Closed;
        }

        /// <summary>
        /// Gets the guarded source.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the stored state. An open breaker whose cooldown has ended is
        /// still <see cref="CircuitState.Open"/> until <see cref="BeginProbe"/>.
        /// </summary>
        public CircuitState State { get; private set; }

        /// <summary>
        /// Gets when a probe may be attempted, while open.
        /// </summary>
        public DateTimeOffset? OpenUntil { get; private set; }

        /// <summary>
        /// Gets the walls hit in a row since the last success.
        /// </summary>
        public int ConsecutiveFailures { get; private set; }

        /// <summary>
        /// Gets the wall times inside the failure window, oldest first.
        /// </summary>
        public IReadOnlyList<DateTimeOffset> RecentFailures => _recentFailures.AsReadOnly();

        /// <summary>
        /// Counts walls inside <see cref="FailureWindow"/> as of
        /// <paramref name="now"/>. The stored list is pruned only on writes,
        /// so readers must use this rather than <see cref="RecentFailures"/>'s count.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>Walls hit within the window before <paramref name="now"/>.</returns>
        public int RecentFailureCount(DateTimeOffset now)
        {
            return _recentFailures.Count(at => now - at < FailureWindow);
        }

        /// <summary>
        /// Creates a closed breaker for a source.
        /// </summary>
        /// <param name="source">The source. Must not be null.</param>
        /// <returns>The breaker.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public static SourceCircuitBreaker Create(SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new SourceCircuitBreaker(source);
        }

        /// <summary>
        /// Gets a value indicating whether a normal run may start: only when
        /// closed. A probe after the cooldown is not a normal run; use
        /// <see cref="CanProbe"/> and <see cref="BeginProbe"/> for that.
        /// </summary>
        public bool AllowsRun => State == CircuitState.Closed;

        /// <summary>
        /// Whether the cooldown has ended so a probe may test the source.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>True when open and the cooldown has elapsed.</returns>
        public bool CanProbe(DateTimeOffset now)
        {
            // OpenUntil is always set while open.
            return State == CircuitState.Open && now >= OpenUntil.GetValueOrDefault();
        }

        /// <summary>
        /// Moves an elapsed open breaker to half-open for one probe run
        /// (one page, no enrichment, operator-approved).
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <exception cref="DomainException">Thrown when not open or the cooldown has not ended.</exception>
        public void BeginProbe(DateTimeOffset now)
        {
            if (!CanProbe(now))
            {
                throw new DomainException(
                    $"Circuit for '{Source}' cannot probe now (state {State})."
                );
            }

            State = CircuitState.HalfOpen;
            OpenUntil = null;
        }

        /// <summary>
        /// Records a run or probe that completed without a wall. A
        /// successful probe closes the breaker and forgets past walls.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <exception cref="DomainException">Thrown when open: no run should have been allowed.</exception>
        public void RecordSuccess(DateTimeOffset now)
        {
            if (State == CircuitState.Open)
            {
                throw new DomainException($"Circuit for '{Source}' is open; no run could succeed.");
            }

            ConsecutiveFailures = 0;
            if (State == CircuitState.HalfOpen)
            {
                State = CircuitState.Closed;
                _recentFailures.Clear();
            }

            Prune(now);
        }

        /// <summary>
        /// Records a wall and opens the breaker when a threshold is reached.
        /// </summary>
        /// <param name="kind">
        /// The wall. <see cref="ChallengeKind.None"/>,
        /// <see cref="ChallengeKind.TransientError"/>, and
        /// <see cref="ChallengeKind.SourceChanged"/> are not walls.
        /// </param>
        /// <param name="now">When the wall was hit.</param>
        /// <exception cref="DomainException">Thrown when <paramref name="kind"/> is not a wall or the breaker is already open.</exception>
        public void RecordFailure(ChallengeKind kind, DateTimeOffset now)
        {
            if (
                kind
                    is ChallengeKind.None
                        or ChallengeKind.TransientError
                        or ChallengeKind.SourceChanged
                || !Enum.IsDefined(kind)
            )
            {
                throw new DomainException($"{kind} is not a wall.");
            }

            if (State == CircuitState.Open)
            {
                throw new DomainException($"Circuit for '{Source}' is already open.");
            }

            ConsecutiveFailures++;
            _recentFailures.Add(now);
            Prune(now);

            if (State == CircuitState.HalfOpen)
            {
                Open(kind, now, RepeatedBlockCooldown);
            }
            else if (
                kind == ChallengeKind.AccessDenied
                || ConsecutiveFailures >= ConsecutiveThreshold
                || _recentFailures.Count >= WindowThreshold
            )
            {
                Open(
                    kind,
                    now,
                    kind == ChallengeKind.RateLimited ? RateLimitCooldown : ChallengeCooldown
                );
            }
        }

        /// <summary>
        /// Opens the breaker and raises <see cref="CircuitOpened"/>.
        /// </summary>
        /// <param name="trigger">The wall that opened it.</param>
        /// <param name="now">The current time.</param>
        /// <param name="cooldown">How long runs stay blocked.</param>
        private void Open(ChallengeKind trigger, DateTimeOffset now, TimeSpan cooldown)
        {
            State = CircuitState.Open;
            OpenUntil = now + cooldown;
            Raise(new CircuitOpened(Source, trigger, now + cooldown, now));
        }

        /// <summary>
        /// Drops wall times older than the window, bounding the list.
        /// </summary>
        /// <param name="now">The current time.</param>
        private void Prune(DateTimeOffset now)
        {
            _recentFailures.RemoveAll(at => now - at >= FailureWindow);
        }
    }
}
