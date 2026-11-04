namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Hard caps for one scrape run: the page window, job and detail
    /// caps, and the jittered delay window between requests. Caps are
    /// politeness limits, not entitlements; the ranges below are the
    /// outer bounds any profile may request (docs/03-domain-model.md §4.1).
    /// </summary>
    public sealed record RunLimits
    {
        /// <summary>
        /// Highest page any profile may request, as a start page or window end.
        /// </summary>
        public const int MaxPagesCeiling = 1000;

        /// <summary>
        /// Highest job cap any profile may request.
        /// </summary>
        public const int MaxJobsCeiling = 20_000;

        /// <summary>
        /// Initializes a new instance, rejecting any value outside its range.
        /// </summary>
        /// <param name="startPage">First result page fetched in a run, 1–1000.</param>
        /// <param name="maxPages">Result pages per run, 1–1000. The window ends at startPage + maxPages - 1.</param>
        /// <param name="maxJobs">Postings collected per run, 1–20 000.</param>
        /// <param name="maxDetails">Detail pages visited per run, 0–1000; 0 disables enrichment.</param>
        /// <param name="minDelay">Shortest delay between requests, 1–60 seconds.</param>
        /// <param name="maxDelay">Longest delay between requests, at least <paramref name="minDelay"/> and at most 2 minutes.</param>
        /// <exception cref="DomainException">Thrown when any value is out of range.</exception>
        public RunLimits(
            int maxPages,
            int maxJobs,
            int maxDetails,
            TimeSpan minDelay,
            TimeSpan maxDelay,
            int startPage = 1
        )
        {
            if (startPage is < 1 or > MaxPagesCeiling)
            {
                throw new DomainException($"startPage must be 1..{MaxPagesCeiling}.");
            }

            if (maxPages is < 1 or > MaxPagesCeiling)
            {
                throw new DomainException($"maxPages must be 1..{MaxPagesCeiling}.");
            }

            if (startPage + maxPages - 1 > MaxPagesCeiling)
            {
                throw new DomainException(
                    $"Pages {startPage}..{startPage + maxPages - 1} exceed the page ceiling of {MaxPagesCeiling}."
                );
            }

            if (maxJobs is < 1 or > MaxJobsCeiling)
            {
                throw new DomainException($"maxJobs must be 1..{MaxJobsCeiling}.");
            }

            if (maxDetails is < 0 or > 1000)
            {
                throw new DomainException("maxDetails must be 0..1000.");
            }

            // A sub-second floor would let jitter collapse into a burst; a
            // floor above a minute is a misconfiguration, not extra safety.
            if (minDelay < TimeSpan.FromSeconds(1) || minDelay > TimeSpan.FromSeconds(60))
            {
                throw new DomainException("minDelay must be 1s..60s.");
            }

            if (maxDelay < minDelay)
            {
                throw new DomainException("maxDelay must be >= minDelay.");
            }

            if (maxDelay > TimeSpan.FromMinutes(2))
            {
                throw new DomainException("maxDelay must be <= 2m.");
            }

            StartPage = startPage;
            MaxPages = maxPages;
            MaxJobs = maxJobs;
            MaxDetails = maxDetails;
            MinDelay = minDelay;
            MaxDelay = maxDelay;
        }

        /// <summary>
        /// Gets the first result page fetched in a run. Page groups split a
        /// site's listing into windows: 1–30, then 31–60.
        /// </summary>
        public int StartPage { get; }

        /// <summary>
        /// Gets the maximum number of result pages fetched in one run.
        /// </summary>
        public int MaxPages { get; }

        /// <summary>
        /// Gets the maximum number of postings collected in one run.
        /// </summary>
        public int MaxJobs { get; }

        /// <summary>
        /// Gets the maximum number of detail pages visited in one run.
        /// Zero means enrichment is disabled.
        /// </summary>
        public int MaxDetails { get; }

        /// <summary>
        /// Gets the shortest delay between two requests.
        /// </summary>
        public TimeSpan MinDelay { get; }

        /// <summary>
        /// Gets the longest delay between two requests.
        /// </summary>
        public TimeSpan MaxDelay { get; }
    }
}
