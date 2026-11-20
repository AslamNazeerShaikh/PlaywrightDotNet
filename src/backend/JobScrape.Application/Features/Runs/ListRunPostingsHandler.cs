using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Lists the postings one run saw, optionally narrowed by location and
    /// company filters, plus the distinct facet values behind those filters.
    /// </summary>
    public sealed class ListRunPostingsHandler
    {
        /// <summary>
        /// How many postings the run detail shows.
        /// </summary>
        public const int Limit = 200;

        // Injected and never mutated.
        private readonly IRunStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListRunPostingsHandler"/> class.
        /// </summary>
        /// <param name="store">The run read store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
        public ListRunPostingsHandler(IRunStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        /// <summary>
        /// Lists up to <paramref name="limit"/> postings for the run,
        /// skipping <paramref name="offset"/> most recent first. Owns the
        /// paging: <paramref name="offset"/> coerces to at least 0 and
        /// <paramref name="limit"/> to 1..500 (non-positive resets to
        /// <see cref="Limit"/>); the transport passes query values through,
        /// and the store rejects out-of-range values. Owns filter
        /// normalization too: each axis is trimmed, blanks are dropped,
        /// duplicates collapse case-insensitively (first spelling wins), and
        /// a null, empty, or all-blank axis is unfiltered. There is no cap:
        /// every surviving value is forwarded.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="offset">How many postings to skip.</param>
        /// <param name="limit">How many postings to return.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <param name="locations">Location filters; null means unfiltered.</param>
        /// <param name="companies">Company filters; null means unfiltered.</param>
        /// <returns>The postings; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<RunPostingListResponse>> HandleAsync(
            Guid runId,
            int offset,
            int limit,
            CancellationToken cancellationToken,
            IReadOnlyList<string>? locations = null,
            IReadOnlyList<string>? companies = null
        )
        {
            if (runId == Guid.Empty)
            {
                return Result.Fail<RunPostingListResponse>(RunIds.Required());
            }

            var postings = await _store.ListPostingsAsync(
                runId,
                Math.Max(0, offset),
                limit <= 0 ? Limit : Math.Clamp(limit, 1, 500),
                cancellationToken,
                Normalize(locations),
                Normalize(companies)
            );
            return postings is null
                ? Result.Fail<RunPostingListResponse>(Errors.RunNotFound(runId))
                : Result.Ok(postings);
        }

        /// <summary>
        /// Lists the distinct location parts across the postings a run saw.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The locations; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<IReadOnlyList<string>>> HandleLocationsAsync(
            Guid runId,
            CancellationToken cancellationToken
        )
        {
            if (runId == Guid.Empty)
            {
                return Result.Fail<IReadOnlyList<string>>(RunIds.Required());
            }

            var locations = await _store.ListPostingLocationsAsync(runId, cancellationToken);
            return locations is null
                ? Result.Fail<IReadOnlyList<string>>(Errors.RunNotFound(runId))
                : Result.Ok(locations);
        }

        /// <summary>
        /// Lists the distinct companies across the postings a run saw.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The companies; <c>VALIDATION_FAILED</c> for an empty id; or <c>RUN_NOT_FOUND</c>.</returns>
        public async Task<Result<IReadOnlyList<string>>> HandleCompaniesAsync(
            Guid runId,
            CancellationToken cancellationToken
        )
        {
            if (runId == Guid.Empty)
            {
                return Result.Fail<IReadOnlyList<string>>(RunIds.Required());
            }

            var companies = await _store.ListPostingCompaniesAsync(runId, cancellationToken);
            return companies is null
                ? Result.Fail<IReadOnlyList<string>>(Errors.RunNotFound(runId))
                : Result.Ok(companies);
        }

        /// <summary>
        /// Trims, drops blanks, and de-duplicates a filter axis
        /// case-insensitively; a null, empty, or all-blank axis is unfiltered.
        /// </summary>
        /// <param name="values">The raw values.</param>
        /// <returns>The surviving values, or null when none survive.</returns>
        private static List<string>? Normalize(IReadOnlyList<string>? values)
        {
            if (values is null)
            {
                return null;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var value in values)
            {
                var trimmed = value?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result.Count == 0 ? null : result;
        }
    }
}
