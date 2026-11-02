using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Aggregate root for one source's usage on one UTC day
    /// (docs/03-domain-model.md §4.5). It stores counts only; caps come from
    /// configuration and are passed into each check. Persisted so a restart
    /// never resets the day; counts never decrease.
    /// </summary>
    public sealed class SourceQuota : AggregateRoot
    {
        /// <summary>
        /// Initializes a new instance with zero counts.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="quotaDate">The UTC day.</param>
        /// <param name="updatedAt">Creation time; also the first update time.</param>
        private SourceQuota(SourceKey source, DateOnly quotaDate, DateTimeOffset updatedAt)
        {
            Source = source;
            QuotaDate = quotaDate;
            UpdatedAt = updatedAt;
        }

        /// <summary>
        /// Gets the source the counts belong to.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the UTC day the counts belong to.
        /// </summary>
        public DateOnly QuotaDate { get; }

        /// <summary>
        /// Gets the result pages fetched today.
        /// </summary>
        public int PagesFetched { get; private set; }

        /// <summary>
        /// Gets the detail pages visited today.
        /// </summary>
        public int DetailsFetched { get; private set; }

        /// <summary>
        /// Gets the runs started today.
        /// </summary>
        public int RunsStarted { get; private set; }

        /// <summary>
        /// Gets the walls hit today.
        /// </summary>
        public int Challenges { get; private set; }

        /// <summary>
        /// Gets when a count last changed.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; private set; }

        /// <summary>
        /// Starts the quota for the UTC day containing <paramref name="now"/>.
        /// </summary>
        /// <param name="source">The source. Must not be null.</param>
        /// <param name="now">The current time; its UTC date becomes the quota day.</param>
        /// <returns>A quota with zero counts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public static SourceQuota StartDay(SourceKey source, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new SourceQuota(source, DateOnly.FromDateTime(now.UtcDateTime), now);
        }

        /// <summary>
        /// Consumes one result page if today's page cap allows it.
        /// </summary>
        /// <param name="cap">Daily page cap, at least 0; 0 blocks all pages.</param>
        /// <param name="now">The current time; must fall on <see cref="QuotaDate"/> (UTC).</param>
        /// <returns>The decision; nothing is consumed when exceeded.</returns>
        /// <exception cref="DomainException">Thrown when the cap is negative or <paramref name="now"/> is another day.</exception>
        public QuotaDecision TryConsumePage(int cap, DateTimeOffset now)
        {
            var decision = Check(QuotaKind.Pages, PagesFetched, cap, now);
            if (decision.IsAllowed)
            {
                PagesFetched = decision.Used;
                UpdatedAt = now;
            }

            return decision;
        }

        /// <summary>
        /// Consumes one detail visit if today's detail cap allows it.
        /// </summary>
        /// <param name="cap">Daily detail cap, at least 0; 0 blocks all details.</param>
        /// <param name="now">The current time; must fall on <see cref="QuotaDate"/> (UTC).</param>
        /// <returns>The decision; nothing is consumed when exceeded.</returns>
        /// <exception cref="DomainException">Thrown when the cap is negative or <paramref name="now"/> is another day.</exception>
        public QuotaDecision TryConsumeDetail(int cap, DateTimeOffset now)
        {
            var decision = Check(QuotaKind.Details, DetailsFetched, cap, now);
            if (decision.IsAllowed)
            {
                DetailsFetched = decision.Used;
                UpdatedAt = now;
            }

            return decision;
        }

        /// <summary>
        /// Counts one run start if today's run cap allows it.
        /// </summary>
        /// <param name="cap">Daily run cap, at least 0; 0 blocks all runs.</param>
        /// <param name="now">The current time; must fall on <see cref="QuotaDate"/> (UTC).</param>
        /// <returns>The decision; nothing is consumed when exceeded.</returns>
        /// <exception cref="DomainException">Thrown when the cap is negative or <paramref name="now"/> is another day.</exception>
        public QuotaDecision TryStartRun(int cap, DateTimeOffset now)
        {
            var decision = Check(QuotaKind.Runs, RunsStarted, cap, now);
            if (decision.IsAllowed)
            {
                RunsStarted = decision.Used;
                UpdatedAt = now;
            }

            return decision;
        }

        /// <summary>
        /// Counts one wall hit today. Walls are recorded, never capped.
        /// </summary>
        /// <param name="now">The current time; must fall on <see cref="QuotaDate"/> (UTC).</param>
        /// <exception cref="DomainException">Thrown when <paramref name="now"/> is another day.</exception>
        public void RecordChallenge(DateTimeOffset now)
        {
            EnsureSameDay(now);
            Challenges++;
            UpdatedAt = now;
        }

        /// <summary>
        /// Decides whether one more unit fits under the cap, without mutating.
        /// </summary>
        /// <param name="kind">The counter.</param>
        /// <param name="used">The current count.</param>
        /// <param name="cap">The daily cap.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The decision with the would-be count.</returns>
        /// <exception cref="DomainException">Thrown when the cap is negative or <paramref name="now"/> is another day.</exception>
        private QuotaDecision Check(QuotaKind kind, int used, int cap, DateTimeOffset now)
        {
            if (cap < 0)
            {
                throw new DomainException($"{kind} cap must be non-negative.");
            }

            EnsureSameDay(now);
            return used < cap
                ? new QuotaDecision(true, kind, cap, used + 1)
                : new QuotaDecision(false, kind, cap, used);
        }

        /// <summary>
        /// Throws when <paramref name="now"/> falls on another UTC day, so a
        /// stale quota is never charged; the caller loads or starts today's.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <exception cref="DomainException">Thrown on a different UTC day.</exception>
        private void EnsureSameDay(DateTimeOffset now)
        {
            if (DateOnly.FromDateTime(now.UtcDateTime) != QuotaDate)
            {
                throw new DomainException($"Quota for {QuotaDate:O} cannot be charged at {now:O}.");
            }
        }
    }
}
