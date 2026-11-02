namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Outcome counts of one fetched result page, recorded into a run.
    /// </summary>
    public sealed record PageStats
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="postingsFound">Valid postings parsed from the page, at least 0.</param>
        /// <param name="postingsNew">Of those, postings not seen before, at least 0.</param>
        /// <param name="postingsUpdated">Of those, known postings whose content changed, at least 0.</param>
        /// <exception cref="DomainException">
        /// Thrown when a count is negative or new plus updated exceeds found.
        /// </exception>
        public PageStats(int postingsFound, int postingsNew, int postingsUpdated)
        {
            if (postingsFound < 0 || postingsNew < 0 || postingsUpdated < 0)
            {
                throw new DomainException("Page counts must be non-negative.");
            }

            if (postingsNew + postingsUpdated > postingsFound)
            {
                throw new DomainException(
                    "New plus updated postings cannot exceed found postings."
                );
            }

            PostingsFound = postingsFound;
            PostingsNew = postingsNew;
            PostingsUpdated = postingsUpdated;
        }

        /// <summary>
        /// Gets the number of valid postings parsed from the page.
        /// </summary>
        public int PostingsFound { get; }

        /// <summary>
        /// Gets the number of postings not seen before.
        /// </summary>
        public int PostingsNew { get; }

        /// <summary>
        /// Gets the number of known postings whose content changed.
        /// </summary>
        public int PostingsUpdated { get; }
    }
}
