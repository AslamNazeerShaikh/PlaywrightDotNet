using JobScrape.Application.Features.Runs;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// EF Core read side of runs: no-tracking queries projected to read
    /// models. Profile names are joined in memory from the few profiles the
    /// page references.
    /// </summary>
    public sealed class RunReadStore : IRunStore
    {
        // Both injected and never mutated; scoped with the unit of work.
        private readonly JobScrapeDbContext _db;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunReadStore"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clock">The time source; a run that never started reports an empty list.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunReadStore(JobScrapeDbContext db, TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(clock);
            _db = db;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<RunResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var run = await FindAsync(id, cancellationToken);
            if (run is null)
            {
                return null;
            }

            var names = await ProfileNamesAsync([run.ProfileId], cancellationToken);
            return RunResponse.From(run, names.GetValueOrDefault(run.ProfileId));
        }

        /// <inheritdoc />
        public async Task<RunListResponse> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken
        )
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);

            var runs = await _db
                .ScrapeRuns.AsNoTracking()
                .OrderByDescending(run => run.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);
            var total = await _db.ScrapeRuns.CountAsync(cancellationToken);
            var names = await ProfileNamesAsync(
                runs.Select(run => run.ProfileId).Distinct().ToArray(),
                cancellationToken
            );
            return new RunListResponse(
                runs.Select(run => RunResponse.From(run, names.GetValueOrDefault(run.ProfileId)))
                    .ToArray(),
                total
            );
        }

        /// <inheritdoc />
        public async Task<RunPostingListResponse?> ListPostingsAsync(
            Guid runId,
            int offset,
            int limit,
            CancellationToken cancellationToken,
            IReadOnlyList<string>? locations = null,
            IReadOnlyList<string>? companies = null
        )
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);

            var postings = await PostingsAsync(
                runId,
                offset,
                limit,
                locations,
                companies,
                cancellationToken
            );
            return postings is null
                ? null
                : new RunPostingListResponse(
                    runId,
                    postings
                        .Items.Select(posting => ToResponse(posting, postings.RunStart))
                        .ToArray(),
                    postings.Total
                );
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>?> ListPostingLocationsAsync(
            Guid runId,
            CancellationToken cancellationToken = default
        )
        {
            var postings = await AllPostingsAsync(runId, cancellationToken);
            if (postings is null)
            {
                return null;
            }

            return DistinctSorted(
                postings.Ordered.SelectMany(posting => SplitLocation(posting.Location?.Raw))
            );
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>?> ListPostingCompaniesAsync(
            Guid runId,
            CancellationToken cancellationToken = default
        )
        {
            var postings = await AllPostingsAsync(runId, cancellationToken);
            if (postings is null)
            {
                return null;
            }

            return DistinctSorted(
                postings
                    .Ordered.Select(posting => posting.Company?.Value)
                    .Where(company => !string.IsNullOrWhiteSpace(company))
                    .Select(company => company!.Trim())
            );
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ExportPosting>?> ListExportPostingsAsync(
            Guid runId,
            int limit,
            CancellationToken cancellationToken
        )
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

            var postings = await PostingsAsync(runId, 0, limit, null, null, cancellationToken);
            return postings?.Items.Select(ToExport).ToArray();
        }

        /// <summary>
        /// Loads one run's postings from its recorded id set, filtered and paged.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="offset">How many postings to skip.</param>
        /// <param name="limit">How many postings to return.</param>
        /// <param name="locations">Location filters; null means unfiltered.</param>
        /// <param name="companies">Company filters; null means unfiltered.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run start and postings, or null when the run does not exist.</returns>
        private async Task<RunPostings?> PostingsAsync(
            Guid runId,
            int offset,
            int limit,
            IReadOnlyList<string>? locations,
            IReadOnlyList<string>? companies,
            CancellationToken cancellationToken
        )
        {
            var loaded = await AllPostingsAsync(runId, cancellationToken);
            if (loaded is null)
            {
                return null;
            }

            var locationFilter = FilterSet(locations);
            var companyFilter = FilterSet(companies);
            var ordered = loaded
                .Ordered.Where(posting =>
                    MatchesLocation(posting, locationFilter)
                    && MatchesCompany(posting, companyFilter)
                )
                .ToList();
            var postings = ordered.Skip(offset).Take(limit).ToList();
            return new RunPostings(loaded.RunStart, postings, ordered.Count);
        }

        /// <summary>
        /// Loads one run's postings from its recorded id set, most recently
        /// seen first, before any caller filtering or paging.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run start and postings, or null when the run does not exist.</returns>
        private async Task<LoadedRunPostings?> AllPostingsAsync(
            Guid runId,
            CancellationToken cancellationToken
        )
        {
            var run = await FindAsync(runId, cancellationToken);
            if (run is null)
            {
                return null;
            }

            if (run.StartedAt is not { } from)
            {
                return new LoadedRunPostings(_clock.GetUtcNow(), []);
            }

            // A run's postings are what its progress recorded, not what the
            // mutable LastSeenAt timestamps still remember (later runs move shared rows).
            // The lower bound keeps out cards the run saw on-page but dropped before
            // persist (skill mismatch): those never advanced LastSeenAt into this run.
            var seen = run.Progress?.SeenExternalIds ?? [];
            if (seen.Count == 0)
            {
                return new LoadedRunPostings(from, []);
            }

            var source = run.Source;
            var onlyNew = run.OnlyNew;
            var ids = seen.Select(ExternalJobId.Create).ToArray();
            // SQLite caps bound parameters (~32k): one id list would 500 on large
            // runs, so fetch in small batches and page the merged set in memory.
            var all = new List<JobPosting>();
            foreach (var chunk in ids.Chunk(2000))
            {
                var batch = await _db
                    .JobPostings.AsNoTracking()
                    .Where(posting =>
                        posting.Source == source
                        && chunk.Contains(posting.ExternalJobId)
                        && posting.LastSeenAt >= from
                        && (!onlyNew || posting.FirstSeenAt >= from)
                    )
                    .ToListAsync(cancellationToken);
                all.AddRange(batch);
            }

            var ordered = all.OrderByDescending(posting => posting.LastSeenAt)
                .ThenBy(posting => posting.ExternalJobId.Value, StringComparer.Ordinal)
                .ToList();
            return new LoadedRunPostings(from, ordered);
        }

        /// <summary>
        /// Normalizes a filter axis: trims, drops blanks, and de-duplicates
        /// case-insensitively. A null, empty, or all-blank axis is unfiltered.
        /// </summary>
        /// <param name="values">The raw values.</param>
        /// <returns>The filter set, or null when nothing survives.</returns>
        private static HashSet<string>? FilterSet(IReadOnlyList<string>? values)
        {
            if (values is null)
            {
                return null;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                var trimmed = value?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    set.Add(trimmed);
                }
            }

            return set.Count == 0 ? null : set;
        }

        /// <summary>
        /// Whether a posting survives the location filter: any
        /// comma-separated part of its raw location equals any filter value.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <param name="filter">The normalized filter; null means unfiltered.</param>
        /// <returns>True when the posting survives.</returns>
        private static bool MatchesLocation(JobPosting posting, HashSet<string>? filter)
        {
            if (filter is null)
            {
                return true;
            }

            return SplitLocation(posting.Location?.Raw).Any(part => filter.Contains(part));
        }

        /// <summary>
        /// Whether a posting survives the company filter: its company equals
        /// any filter value exactly.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <param name="filter">The normalized filter; null means unfiltered.</param>
        /// <returns>True when the posting survives.</returns>
        private static bool MatchesCompany(JobPosting posting, HashSet<string>? filter)
        {
            if (filter is null)
            {
                return true;
            }

            var company = posting.Company?.Value;
            return company is not null && filter.Contains(company);
        }

        /// <summary>
        /// Splits a raw location on commas into trimmed, non-blank parts.
        /// </summary>
        /// <param name="raw">The raw location text.</param>
        /// <returns>The parts; empty when there is no usable text.</returns>
        private static IEnumerable<string> SplitLocation(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                yield break;
            }

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        /// <summary>
        /// De-duplicates values case-insensitively (first spelling wins) and
        /// sorts them case-insensitively with an ordinal tiebreak.
        /// </summary>
        /// <param name="values">The raw values.</param>
        /// <returns>The distinct, sorted values.</returns>
        private static List<string> DistinctSorted(IEnumerable<string> values)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distinct = new List<string>();
            foreach (var value in values)
            {
                if (seen.Add(value))
                {
                    distinct.Add(value);
                }
            }

            distinct.Sort(
                (left, right) =>
                {
                    var primary = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                    return primary != 0
                        ? primary
                        : string.Compare(left, right, StringComparison.Ordinal);
                }
            );
            return distinct;
        }

        /// <summary>
        /// Loads one run without tracking.
        /// </summary>
        /// <param name="id">The run identifier; empty finds nothing.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run, or null.</returns>
        private async Task<ScrapeRun?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            var key = ScrapeRunId.From(id);
            return await _db
                .ScrapeRuns.AsNoTracking()
                .FirstOrDefaultAsync(run => run.Id == key, cancellationToken);
        }

        /// <summary>
        /// Looks up profile names by id; a deleted profile has no entry.
        /// </summary>
        /// <param name="ids">The profile ids.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>Names keyed by profile id.</returns>
        private async Task<Dictionary<SearchProfileId, string>> ProfileNamesAsync(
            SearchProfileId[] ids,
            CancellationToken cancellationToken
        )
        {
            if (ids.Length == 0)
            {
                return [];
            }

            var profiles = await _db
                .SearchProfiles.AsNoTracking()
                .Where(profile => ids.Contains(profile.Id))
                .Select(profile => new { profile.Id, profile.Name })
                .ToListAsync(cancellationToken);
            return profiles.ToDictionary(profile => profile.Id, profile => profile.Name);
        }

        /// <summary>
        /// Maps a posting to its run read model.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <param name="runStart">When the run started; postings first seen since are new.</param>
        /// <returns>The read model.</returns>
        private static RunPostingResponse ToResponse(JobPosting posting, DateTimeOffset runStart)
        {
            return new RunPostingResponse(
                posting.Id.Value,
                posting.ExternalJobId.Value,
                posting.Url.Value,
                posting.Title.Value,
                posting.Company?.Value,
                posting.Location?.Raw,
                (posting.Location?.WorkMode ?? WorkMode.Unknown).ToString(),
                posting.ExperienceRaw,
                posting.SalaryRaw,
                posting.PostedAt,
                posting.Skills.Select(skill => skill.Name).ToArray(),
                posting.Description is not null,
                posting.FirstSeenAt >= runStart,
                posting.FirstSeenAt,
                posting.LastSeenAt
            );
        }

        /// <summary>
        /// Maps a posting to its export model: every stored field.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <returns>The export model.</returns>
        private static ExportPosting ToExport(JobPosting posting)
        {
            return new ExportPosting(
                posting.Id.Value,
                posting.ExternalJobId.Value,
                posting.Source.Value,
                posting.Url.Value,
                posting.ApplyUrl == posting.Url.Value ? null : posting.ApplyUrl,
                posting.Title.Value,
                posting.Company?.Value,
                posting.Location?.Raw,
                (posting.Location?.WorkMode ?? WorkMode.Unknown).ToString(),
                posting.ExperienceRaw,
                posting.SalaryRaw,
                posting.PostedAt,
                posting.Skills.Select(skill => skill.Name).ToArray(),
                posting.Description,
                posting.FirstSeenAt,
                posting.LastSeenAt
            );
        }

        /// <summary>
        /// One run's recorded id set and the postings it still resolves to,
        /// most recently seen first, before caller filtering and paging.
        /// </summary>
        /// <param name="RunStart">When the run started.</param>
        /// <param name="Ordered">The postings, most recently seen first.</param>
        private sealed record LoadedRunPostings(
            DateTimeOffset RunStart,
            IReadOnlyList<JobPosting> Ordered
        );

        /// <summary>
        /// One run's recorded id set and the postings it still resolves to.
        /// </summary>
        /// <param name="RunStart">When the run started.</param>
        /// <param name="Items">The postings, most recently seen first.</param>
        /// <param name="Total">How many of the id set's postings still exist.</param>
        private sealed record RunPostings(
            DateTimeOffset RunStart,
            IReadOnlyList<JobPosting> Items,
            int Total
        );
    }
}
