using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace JobScrape.Application.Tests.Scraping
{
    /// <summary>
    /// Wires a RunScrapeHandler to hand-written in-memory fakes.
    /// </summary>
    internal sealed class ScrapeHarness
    {
        public static readonly RunLimits DefaultLimits = new(
            3,
            60,
            60,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(6)
        );

        public static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        public ScrapeHarness(
            SourceKey? source = null,
            int maxPages = 3,
            int maxJobs = 60,
            int maxDetails = 0
        )
        {
            var key = source ?? SourceKey.Naukri;
            Profile = SearchProfile.Create(
                "backend",
                key,
                new SearchCriteria("c#", ["Pune"], null, null, remoteOnly: false),
                new RunLimits(
                    maxPages,
                    maxJobs,
                    maxDetails,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(6)
                ),
                Start
            );
            Browser = BrowserProfile.Create(
                "main",
                key,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            Browser.MarkVerified(Start);
            Store.SearchProfiles.Add(Profile);
            Store.BrowserProfiles.Add(Browser);
            Source = new ScriptedSource(key);
            Settings = new SourceSettings(
                key,
                true,
                25,
                60,
                2,
                TimeSpan.FromMinutes(45),
                DefaultLimits
            );
            Consent = new ComplianceConsent(true, true, true);
        }

        public FakeTimeProvider Clock { get; } = new(Start);

        public InMemoryStore Store { get; } = new();

        public SearchProfile Profile { get; }

        public BrowserProfile Browser { get; }

        public ScriptedSource Source { get; }

        public FakeSessions Sessions { get; } = new();

        public RecordingArtifacts Artifacts { get; } = new();

        public RecordingDelay Delay { get; } = new();

        public CapturingLogger Logger { get; } = new();

        public SourceSettings Settings { get; set; }

        public ComplianceConsent Consent { get; set; }

        public bool RegisterSource { get; set; } = true;

        public RunScrapeRequest Request => new(Profile.Id, Browser.Id, Consent);

        public RunScrapeHandler Build()
        {
            return new RunScrapeHandler(
                Store.AsStores(),
                new JobSourceRegistry(RegisterSource ? [Source] : []),
                new SourceSettingsCatalog([Settings]),
                Sessions,
                Artifacts,
                Delay,
                new RetryPolicy(2, new MinRandom()),
                new MinRandom(),
                Clock,
                Logger
            );
        }

        public static JobSnapshot Job(
            string id,
            string title = "Engineer",
            SourceKey? source = null
        )
        {
            return new JobSnapshot
            {
                Source = source ?? SourceKey.Naukri,
                ExternalJobId = id,
                Url = $"https://www.naukri.com/job-listings-x-{id}",
                Title = title,
            };
        }

        public static SearchPageResult Page(string? next, params JobSnapshot[] jobs)
        {
            return new SearchPageResult(
                jobs,
                next,
                jobs.Length > 0 ? new PageFingerprint(jobs[0].ExternalJobId, null) : null,
                ChallengeAssessment.Clear
            );
        }

        public static JobDetailResult Detail(string id, string description = "Build APIs.")
        {
            return new JobDetailResult(
                Job(id) with
                {
                    Description = description,
                    Skills = ["C#"],
                },
                ChallengeAssessment.Clear
            );
        }

        public static JobDetailResult DetailWall(ChallengeKind kind)
        {
            return new JobDetailResult(null, new ChallengeAssessment(kind, 1, $"evidence:{kind}"));
        }

        public static SearchPageResult Wall(ChallengeKind kind, TimeSpan? retryAfter = null)
        {
            return SearchPageResult.Blocked(
                new ChallengeAssessment(kind, 1, $"evidence:{kind}", retryAfter)
            );
        }

        internal sealed class InMemoryStore
            : ISearchProfileRepository,
                IScrapeRunRepository,
                IJobPostingRepository,
                IBrowserProfileRepository,
                ISourceQuotaRepository,
                ISourceCircuitBreakerRepository,
                IUnitOfWork
        {
            public List<SearchProfile> SearchProfiles { get; } = [];

            public List<ScrapeRun> Runs { get; } = [];

            public List<JobPosting> Postings { get; } = [];

            public List<BrowserProfile> BrowserProfiles { get; } = [];

            public List<SourceQuota> Quotas { get; } = [];

            public List<SourceCircuitBreaker> Circuits { get; } = [];

            public int Saves { get; private set; }

            public int CancelAtSave { get; set; }

            public int FailAtSave { get; set; }

            public CancellationTokenSource? CancelOnSave { get; set; }

            public ScrapeStores AsStores()
            {
                return new ScrapeStores(this, this, this, this, this, this, this);
            }

            public Task<SearchProfile?> GetAsync(
                SearchProfileId id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(SearchProfiles.FirstOrDefault(p => p.Id == id));
            }

            public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken)
            {
                return Task.FromResult(SearchProfiles.Any(p => p.Name == name));
            }

            public Task AddAsync(SearchProfile profile, CancellationToken cancellationToken)
            {
                SearchProfiles.Add(profile);
                return Task.CompletedTask;
            }

            public Task<ScrapeRun?> GetAsync(ScrapeRunId id, CancellationToken cancellationToken)
            {
                return Task.FromResult(Runs.FirstOrDefault(r => r.Id == id));
            }

            public Task AddAsync(ScrapeRun run, CancellationToken cancellationToken)
            {
                Runs.Add(run);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<ScrapeRun>> ListExecutingAsync(
                CancellationToken cancellationToken
            )
            {
                IReadOnlyList<ScrapeRun> executing = Runs.Where(r =>
                        !r.IsTerminal
                        && r.Stage is not (RunStage.PausedForChallenge or RunStage.RateLimited)
                    )
                    .ToArray();
                return Task.FromResult(executing);
            }

            public Task<JobPosting?> FindAsync(
                SourceKey source,
                ExternalJobId externalJobId,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(
                    Postings.FirstOrDefault(p =>
                        p.Source == source && p.ExternalJobId == externalJobId
                    )
                );
            }

            public Task AddAsync(JobPosting posting, CancellationToken cancellationToken)
            {
                Postings.Add(posting);
                return Task.CompletedTask;
            }

            // Explicit: the search-profile port declares the same signature.
            Task<bool> IBrowserProfileRepository.NameExistsAsync(
                string name,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(BrowserProfiles.Any(p => p.Name == name));
            }

            public Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken)
            {
                BrowserProfiles.Add(profile);
                return Task.CompletedTask;
            }

            public Task<BrowserProfile?> GetAsync(
                BrowserProfileId id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(BrowserProfiles.FirstOrDefault(p => p.Id == id));
            }

            public Task<SourceQuota?> FindAsync(
                SourceKey source,
                DateOnly quotaDate,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(
                    Quotas.FirstOrDefault(q => q.Source == source && q.QuotaDate == quotaDate)
                );
            }

            public Task AddAsync(SourceQuota quota, CancellationToken cancellationToken)
            {
                Quotas.Add(quota);
                return Task.CompletedTask;
            }

            public Task<SourceCircuitBreaker?> FindAsync(
                SourceKey source,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(Circuits.FirstOrDefault(c => c.Source == source));
            }

            public Task AddAsync(SourceCircuitBreaker breaker, CancellationToken cancellationToken)
            {
                Circuits.Add(breaker);
                return Task.CompletedTask;
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                Saves++;
                if (Saves == CancelAtSave)
                {
                    CancelOnSave?.Cancel();
                }

                if (Saves == FailAtSave)
                {
                    throw new InvalidOperationException("database unavailable");
                }

                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(0);
            }
        }

        internal sealed class ScriptedSource(SourceKey key) : IJobSource
        {
            private readonly Queue<object> _pages = new();

            private readonly Queue<JobDetailResult> _details = new();

            public SourceKey Key { get; } = key;

            public SourceCapabilities Capabilities => new(SupportsDetails);

            public List<SearchPageRequest> Requests { get; } = [];

            public ScriptedSource Then(params SearchPageResult[] pages)
            {
                foreach (var page in pages)
                {
                    _pages.Enqueue(page);
                }

                return this;
            }

            public ScriptedSource ThenThrow(Exception exception)
            {
                _pages.Enqueue(exception);
                return this;
            }

            public Task<SearchPageResult> SearchPageAsync(
                SourceRunContext context,
                SearchPageRequest request,
                CancellationToken cancellationToken
            )
            {
                Requests.Add(request);
                var next = _pages.Dequeue();
                return next is Exception exception
                    ? throw exception
                    : Task.FromResult((SearchPageResult)next);
            }

            public bool SupportsDetails { get; set; } = true;

            public List<JobReference> DetailRequests { get; } = [];

            public ScriptedSource ThenDetails(params JobDetailResult[] details)
            {
                foreach (var detail in details)
                {
                    _details.Enqueue(detail);
                }

                return this;
            }

            public Task<JobDetailResult> GetDetailAsync(
                SourceRunContext context,
                JobReference job,
                CancellationToken cancellationToken
            )
            {
                DetailRequests.Add(job);
                return Task.FromResult(_details.Dequeue());
            }
        }

        internal sealed class FakeSessions : ISessionManager
        {
            public ChallengeAssessment Result { get; set; } = ChallengeAssessment.Clear;

            public int Calls { get; private set; }

            public Task<ChallengeAssessment> EnsureSessionAsync(
                SourceKey source,
                BrowserProfileId profileId,
                CancellationToken cancellationToken
            )
            {
                Calls++;
                return Task.FromResult(Result);
            }
        }

        internal sealed class RecordingArtifacts : IArtifactStore
        {
            public List<ChallengeAssessment> Captured { get; } = [];

            public Task<string> CaptureAsync(
                SourceRunContext context,
                ChallengeAssessment assessment,
                CancellationToken cancellationToken
            )
            {
                Captured.Add(assessment);
                return Task.FromResult($"runs/{context.RunId}");
            }
        }

        internal sealed class RecordingDelay : IDelay
        {
            public List<TimeSpan> Waits { get; } = [];

            public CancellationTokenSource? CancelOnWait { get; set; }

            public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
            {
                Waits.Add(duration);
                CancelOnWait?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        internal sealed class CapturingLogger : ILogger<RunScrapeHandler>
        {
            public List<LogEntry> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                    : [];
                Entries.Add(
                    new LogEntry(logLevel, formatter(state, exception), properties, exception)
                );
            }
        }

        internal sealed record LogEntry(
            LogLevel Level,
            string Message,
            Dictionary<string, object?> Properties,
            Exception? Exception
        );

        internal sealed class MinRandom : IRandomSource
        {
            public int NextInt32(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
