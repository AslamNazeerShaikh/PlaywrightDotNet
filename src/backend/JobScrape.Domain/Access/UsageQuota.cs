using JobScrape.Domain.Abstractions;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The admin-managed definition for one <see cref="QuotaScope"/>: how
    /// many requests one authenticated user may make per window. Stores the
    /// definition only. Enforcement counters live in memory at the API edge
    /// and reset on restart by design; this row survives restarts.
    /// </summary>
    public sealed class UsageQuota : AggregateRoot
    {
        /// <summary>
        /// The longest window an admin may configure.
        /// </summary>
        public static readonly TimeSpan MaxWindow = TimeSpan.FromHours(24);

        /// <summary>
        /// Initializes a new instance. Private so definitions are created
        /// through the validating <see cref="Create"/>.
        /// </summary>
        /// <param name="scope">The governed surface.</param>
        /// <param name="limit">Requests per window, at least 1.</param>
        /// <param name="window">The fixed window, positive, at most 24 hours.</param>
        private UsageQuota(QuotaScope scope, int limit, TimeSpan window)
        {
            Scope = scope;
            Limit = limit;
            Window = window;
        }

        /// <summary>
        /// Gets the governed surface. The primary key.
        /// </summary>
        public QuotaScope Scope { get; }

        /// <summary>
        /// Gets the requests one user may make per window.
        /// </summary>
        public int Limit { get; private set; }

        /// <summary>
        /// Gets the fixed window the limit applies to.
        /// </summary>
        public TimeSpan Window { get; private set; }

        /// <summary>
        /// Creates the definition for one scope.
        /// </summary>
        /// <param name="scope">The governed surface.</param>
        /// <param name="limit">Requests per window. Must be positive.</param>
        /// <param name="window">The fixed window. Must be positive and at most 24 hours.</param>
        /// <returns>The definition.</returns>
        /// <exception cref="DomainException">Thrown when the limit or window is out of range.</exception>
        public static UsageQuota Create(QuotaScope scope, int limit, TimeSpan window)
        {
            return new UsageQuota(scope, CheckLimit(limit), CheckWindow(window));
        }

        /// <summary>
        /// Changes the request limit.
        /// </summary>
        /// <param name="limit">Requests per window. Must be positive.</param>
        /// <exception cref="DomainException">Thrown when the limit is not positive.</exception>
        public void SetLimit(int limit)
        {
            Limit = CheckLimit(limit);
        }

        /// <summary>
        /// Changes the fixed window.
        /// </summary>
        /// <param name="window">The fixed window. Must be positive and at most 24 hours.</param>
        /// <exception cref="DomainException">Thrown when the window is out of range.</exception>
        public void SetWindow(TimeSpan window)
        {
            Window = CheckWindow(window);
        }

        /// <summary>
        /// Validates a request limit.
        /// </summary>
        /// <param name="limit">The candidate limit.</param>
        /// <returns>The limit.</returns>
        /// <exception cref="DomainException">Thrown when the limit is not positive.</exception>
        private static int CheckLimit(int limit)
        {
            if (limit < 1)
            {
                throw new DomainException("Usage quota limit must be positive.");
            }

            return limit;
        }

        /// <summary>
        /// Validates a fixed window.
        /// </summary>
        /// <param name="window">The candidate window.</param>
        /// <returns>The window.</returns>
        /// <exception cref="DomainException">Thrown when the window is not positive or exceeds 24 hours.</exception>
        private static TimeSpan CheckWindow(TimeSpan window)
        {
            if (window <= TimeSpan.Zero || window > MaxWindow)
            {
                throw new DomainException(
                    "Usage quota window must be positive and at most 24 hours."
                );
            }

            return window;
        }
    }
}
