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
        /// <param name="clock">The time source; ends the posting window of a run still executing.</param>
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
        public async Task<RunListResponse> ListAsync(int limit, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

            var runs = await _db
                .ScrapeRuns.AsNoTracking()
                .OrderByDescending(run => run.CreatedAt)
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
            int limit,
            CancellationToken cancellationToken
        )
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

            var run = await FindAsync(runId, cancellationToken);
            if (run is null)
            {
                return null;
            }

            if (run.StartedAt is not { } from)
            {
                return new RunPostingListResponse(runId, []);
            }

            var until = run.FinishedAt ?? _clock.GetUtcNow();
            var source = run.Source;
            var postings = await _db
                .JobPostings.AsNoTracking()
                .Where(posting =>
                    posting.Source == source
                    && posting.LastSeenAt >= from
                    && posting.LastSeenAt <= until
                )
                .OrderByDescending(posting => posting.LastSeenAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
            return new RunPostingListResponse(
                runId,
                postings.Select(posting => ToResponse(posting, from)).ToArray()
            );
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
    }
}
