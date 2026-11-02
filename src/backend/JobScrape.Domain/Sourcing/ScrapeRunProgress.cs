using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// The resumable position of one scrape run: pages completed, the
    /// adapter cursor for the next page, and the external ids already
    /// counted. Saved after every fetched page, so an interrupted run can
    /// continue without recounting or refetching what it already saw.
    /// </summary>
    public sealed class ScrapeRunProgress
    {
        /// <summary>
        /// Initializes a new instance with its run key.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="pagesCompleted">Pages fetched so far.</param>
        /// <param name="nextCursor">The adapter cursor for the next page, if any.</param>
        /// <param name="seenExternalIds">External ids already counted in this run.</param>
        /// <param name="updatedAt">When the position was recorded.</param>
        private ScrapeRunProgress(
            ScrapeRunId id,
            int pagesCompleted,
            string? nextCursor,
            IReadOnlyList<string> seenExternalIds,
            DateTimeOffset updatedAt
        )
        {
            Id = id;
            PagesCompleted = pagesCompleted;
            NextCursor = nextCursor;
            SeenExternalIds = seenExternalIds;
            UpdatedAt = updatedAt;
        }

        /// <summary>
        /// Gets the run this position belongs to. Also the storage key.
        /// </summary>
        public ScrapeRunId Id { get; }

        /// <summary>
        /// Gets how many result pages were fetched.
        /// </summary>
        public int PagesCompleted { get; private set; }

        /// <summary>
        /// Gets the adapter cursor for the next page, if any.
        /// </summary>
        public string? NextCursor { get; private set; }

        /// <summary>
        /// Gets the external ids already counted in this run.
        /// </summary>
        public IReadOnlyList<string> SeenExternalIds { get; private set; }

        /// <summary>
        /// Gets when the position was last recorded.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; private set; }

        /// <summary>
        /// Starts a position for a run that has fetched nothing yet.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="now">The start time.</param>
        /// <returns>The initial position.</returns>
        public static ScrapeRunProgress Start(ScrapeRunId id, DateTimeOffset now)
        {
            return new ScrapeRunProgress(id, 0, null, [], now);
        }

        /// <summary>
        /// Records one fetched page: advances the page count and replaces
        /// the cursor and seen ids with the run's current ones.
        /// </summary>
        /// <param name="pagesCompleted">Total pages fetched, not below the current count.</param>
        /// <param name="nextCursor">The adapter cursor for the next page, if any.</param>
        /// <param name="seenExternalIds">External ids counted so far. Must not be null.</param>
        /// <param name="now">When the page was recorded.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="seenExternalIds"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when <paramref name="pagesCompleted"/> moves backward.</exception>
        public void RecordPage(
            int pagesCompleted,
            string? nextCursor,
            IEnumerable<string> seenExternalIds,
            DateTimeOffset now
        )
        {
            ArgumentNullException.ThrowIfNull(seenExternalIds);
            if (pagesCompleted < PagesCompleted)
            {
                throw new DomainException("Progress pages cannot move backward.");
            }

            PagesCompleted = pagesCompleted;
            NextCursor = nextCursor;
            SeenExternalIds = seenExternalIds.ToArray();
            UpdatedAt = now;
        }
    }
}
