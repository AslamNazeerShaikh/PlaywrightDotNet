namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Counters of one scrape run. Immutable and monotonic: every change
    /// returns a new instance with larger counts, and there is no reset.
    /// </summary>
    public sealed record RunStats
    {
        /// <summary>
        /// Stats of a run that has done nothing yet.
        /// </summary>
        public static readonly RunStats Empty = new(0, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Rehydrates stored counters. Run code grows counts through the
        /// <c>Add</c> methods; this is for loading a saved run.
        /// </summary>
        /// <param name="pagesFetched">Result pages fetched.</param>
        /// <param name="postingsFound">Postings parsed.</param>
        /// <param name="postingsNew">Postings not seen before.</param>
        /// <param name="postingsUpdated">Known postings whose content changed.</param>
        /// <param name="detailsFetched">Detail pages visited.</param>
        /// <param name="challenges">Walls that paused the run.</param>
        /// <param name="failures">Pages or details that failed and were skipped.</param>
        /// <param name="postedDateMismatches">Card/detail posted-date disagreements.</param>
        /// <returns>The stats.</returns>
        /// <exception cref="DomainException">Thrown when a count is negative or new plus updated exceeds found.</exception>
        public static RunStats Restore(
            int pagesFetched,
            int postingsFound,
            int postingsNew,
            int postingsUpdated,
            int detailsFetched,
            int challenges,
            int failures,
            int postedDateMismatches
        )
        {
            if (pagesFetched < 0 || detailsFetched < 0 || challenges < 0 || failures < 0)
            {
                throw new DomainException("Run counts must be non-negative.");
            }

            if (postedDateMismatches < 0)
            {
                throw new DomainException("Posted-date mismatches must be non-negative.");
            }

            // PageStats enforces the posting-count invariants.
            var postings = new PageStats(postingsFound, postingsNew, postingsUpdated);
            return new RunStats(
                pagesFetched,
                postings.PostingsFound,
                postings.PostingsNew,
                postings.PostingsUpdated,
                detailsFetched,
                challenges,
                failures,
                postedDateMismatches
            );
        }

        /// <summary>
        /// Initializes a new instance. Private so counts only grow through
        /// the <c>Add</c> methods.
        /// </summary>
        /// <param name="pagesFetched">Result pages fetched.</param>
        /// <param name="postingsFound">Postings parsed.</param>
        /// <param name="postingsNew">Postings not seen before.</param>
        /// <param name="postingsUpdated">Known postings whose content changed.</param>
        /// <param name="detailsFetched">Detail pages visited.</param>
        /// <param name="challenges">Walls that paused the run.</param>
        /// <param name="failures">Pages or details that failed and were skipped.</param>
        /// <param name="postedDateMismatches">Card/detail posted-date disagreements.</param>
        private RunStats(
            int pagesFetched,
            int postingsFound,
            int postingsNew,
            int postingsUpdated,
            int detailsFetched,
            int challenges,
            int failures,
            int postedDateMismatches
        )
        {
            PagesFetched = pagesFetched;
            PostingsFound = postingsFound;
            PostingsNew = postingsNew;
            PostingsUpdated = postingsUpdated;
            DetailsFetched = detailsFetched;
            Challenges = challenges;
            Failures = failures;
            PostedDateMismatches = postedDateMismatches;
        }

        /// <summary>
        /// Gets the number of result pages fetched.
        /// </summary>
        public int PagesFetched { get; }

        /// <summary>
        /// Gets the number of postings parsed.
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

        /// <summary>
        /// Gets the number of detail pages visited.
        /// </summary>
        public int DetailsFetched { get; }

        /// <summary>
        /// Gets the number of walls that paused the run.
        /// </summary>
        public int Challenges { get; }

        /// <summary>
        /// Gets the number of pages or details that failed and were skipped.
        /// </summary>
        public int Failures { get; }

        /// <summary>
        /// Gets the number of card/detail posted-date disagreements seen
        /// while enriching. The detail date wins each merge.
        /// </summary>
        public int PostedDateMismatches { get; }

        /// <summary>
        /// Adds one fetched page and its posting counts.
        /// </summary>
        /// <param name="page">The page outcome. Must not be null.</param>
        /// <returns>The updated stats.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
        public RunStats AddPage(PageStats page)
        {
            ArgumentNullException.ThrowIfNull(page);
            return new RunStats(
                PagesFetched + 1,
                PostingsFound + page.PostingsFound,
                PostingsNew + page.PostingsNew,
                PostingsUpdated + page.PostingsUpdated,
                DetailsFetched,
                Challenges,
                Failures,
                PostedDateMismatches
            );
        }

        /// <summary>
        /// Adds one visited detail page.
        /// </summary>
        /// <returns>The updated stats.</returns>
        public RunStats AddDetail()
        {
            return new RunStats(
                PagesFetched,
                PostingsFound,
                PostingsNew,
                PostingsUpdated,
                DetailsFetched + 1,
                Challenges,
                Failures,
                PostedDateMismatches
            );
        }

        /// <summary>
        /// Adds one wall that paused the run.
        /// </summary>
        /// <returns>The updated stats.</returns>
        public RunStats AddChallenge()
        {
            return new RunStats(
                PagesFetched,
                PostingsFound,
                PostingsNew,
                PostingsUpdated,
                DetailsFetched,
                Challenges + 1,
                Failures,
                PostedDateMismatches
            );
        }

        /// <summary>
        /// Adds one page or detail that failed and was skipped.
        /// </summary>
        /// <returns>The updated stats.</returns>
        public RunStats AddFailure()
        {
            return new RunStats(
                PagesFetched,
                PostingsFound,
                PostingsNew,
                PostingsUpdated,
                DetailsFetched,
                Challenges,
                Failures + 1,
                PostedDateMismatches
            );
        }

        /// <summary>
        /// Adds one card/detail posted-date disagreement seen while enriching.
        /// </summary>
        /// <returns>The updated stats.</returns>
        public RunStats AddPostedDateMismatch()
        {
            return new RunStats(
                PagesFetched,
                PostingsFound,
                PostingsNew,
                PostingsUpdated,
                DetailsFetched,
                Challenges,
                Failures,
                PostedDateMismatches + 1
            );
        }
    }
}
