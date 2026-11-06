using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class RunActionsToolsTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly ComplianceConsent Consent = new(true, false, false);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static RunResponse Run(Guid? id = null)
        {
            return new RunResponse(
                id ?? Guid.NewGuid(),
                Guid.NewGuid(),
                "backend",
                [],
                "naukri",
                "Completed",
                true,
                3,
                60,
                60,
                0,
                60,
                0,
                0,
                3,
                1,
                60,
                60,
                null,
                null,
                null,
                Now,
                Now,
                Now
            );
        }

        private static RunActionsTools Tools(
            FakeLauncher launcher,
            FakeRunStore store,
            FakeRuns runs,
            FakeUnitOfWork unitOfWork,
            ICallerContext? caller = null
        )
        {
            return new RunActionsTools(
                new StartRunHandler(launcher, store, Consent, CallerContext.LocalOperator),
                new CancelRunHandler(
                    launcher,
                    runs,
                    unitOfWork,
                    store,
                    new FixedClock(Now),
                    CallerContext.LocalOperator
                ),
                caller ?? new FakeCaller()
            );
        }

        private static RunActionsTools StartTools(FakeLauncher launcher, FakeRunStore store)
        {
            return Tools(launcher, store, new FakeRuns(), new FakeUnitOfWork());
        }

        [Fact]
        public async Task StartRunReturnsTheRecordedRun()
        {
            var id = Guid.NewGuid();
            var launcher = new FakeLauncher { Next = Result.Ok(id) };
            var store = new FakeRunStore(Run(id));
            var profileId = Guid.NewGuid();
            var browserId = Guid.NewGuid();

            var response = await StartTools(launcher, store)
                .StartRunAsync(profileId.ToString(), browserId.ToString(), true, Ct);

            Assert.Equal(id, response.Id);
            var sent = Assert.Single(launcher.Started);
            Assert.Equal(profileId, sent.ProfileId.Value);
            Assert.Equal(browserId, sent.BrowserProfileId.Value);
            Assert.Same(Consent, sent.Consent);
            Assert.True(sent.Enrich);
        }

        [Fact]
        public async Task StartRunBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(new FakeLauncher(), new FakeRunStore())
                    .StartRunAsync("not-a-guid", Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task StartRunBadBrowserFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(new FakeLauncher(), new FakeRunStore())
                    .StartRunAsync(Guid.NewGuid().ToString(), "not-a-guid", true, Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task StartRunEmptyIdsFailValidation()
        {
            var launcher = new FakeLauncher();
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(launcher, new FakeRunStore())
                    .StartRunAsync(Guid.Empty.ToString(), Guid.Empty.ToString(), true, Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
            Assert.Empty(launcher.Started);
        }

        [Fact]
        public async Task StartRunInProgressFailsConflict()
        {
            var launcher = new FakeLauncher
            {
                Next = Result.Fail<Guid>(Errors.RunInProgress(null)),
            };

            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(launcher, new FakeRunStore())
                    .StartRunAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("RUN_IN_PROGRESS:", error.Message);
        }

        [Fact]
        public async Task StartRunMissingProfileFailsNotFound()
        {
            var profileId = Guid.NewGuid();
            var launcher = new FakeLauncher
            {
                Next = Result.Fail<Guid>(Errors.ProfileNotFound(profileId)),
            };

            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(launcher, new FakeRunStore())
                    .StartRunAsync(profileId.ToString(), Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("PROFILE_NOT_FOUND:", error.Message);
        }

        [Fact]
        public async Task StartRunQuotaExceededFailsQuota()
        {
            var launcher = new FakeLauncher
            {
                Next = Result.Fail<Guid>(
                    Errors.QuotaExceeded(
                        SourceKey.Naukri,
                        new QuotaDecision(false, QuotaKind.Runs, 2, 2)
                    )
                ),
            };

            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(launcher, new FakeRunStore())
                    .StartRunAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("QUOTA_EXCEEDED:", error.Message);
        }

        [Fact]
        public async Task StartRunCoolingDownFailsCoolingDown()
        {
            var launcher = new FakeLauncher
            {
                Next = Result.Fail<Guid>(Errors.SourceCoolingDown(SourceKey.Naukri, null)),
            };

            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(launcher, new FakeRunStore())
                    .StartRunAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("SOURCE_COOLING_DOWN:", error.Message);
        }

        [Fact]
        public async Task CancelRunSignalsAnExecutingRun()
        {
            var run = Run();
            var launcher = new FakeLauncher { Executing = run.Id };
            var runs = new FakeRuns();
            var unitOfWork = new FakeUnitOfWork();

            var response = await Tools(launcher, new FakeRunStore(run), runs, unitOfWork)
                .CancelRunAsync(run.Id.ToString(), Ct);

            Assert.Equal(run, response);
            Assert.Equal([run.Id], launcher.Cancelled);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task CancelRunIsIdempotent()
        {
            var run = Run();
            var tools = Tools(
                new FakeLauncher { Executing = run.Id },
                new FakeRunStore(run),
                new FakeRuns(),
                new FakeUnitOfWork()
            );

            Assert.Equal(run, await tools.CancelRunAsync(run.Id.ToString(), Ct));
            Assert.Equal(run, await tools.CancelRunAsync(run.Id.ToString(), Ct));
        }

        [Fact]
        public async Task CancelRunBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(new FakeLauncher(), new FakeRunStore()).CancelRunAsync("not-a-guid", Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task CancelRunEmptyIdFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(new FakeLauncher(), new FakeRunStore())
                    .CancelRunAsync(Guid.Empty.ToString(), Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task CancelRunMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                StartTools(new FakeLauncher(), new FakeRunStore())
                    .CancelRunAsync(Guid.NewGuid().ToString(), Ct)
            );

            Assert.StartsWith("RUN_NOT_FOUND:", error.Message);
        }

        [Fact]
        public async Task StartRunWithoutWritePermissionIsDenied()
        {
            var viewer = new FakeCaller { Permissions = ["runs.read"] };
            var tools = Tools(
                new FakeLauncher(),
                new FakeRunStore(),
                new FakeRuns(),
                new FakeUnitOfWork(),
                viewer
            );

            var error = await Assert.ThrowsAsync<McpException>(() =>
                tools.StartRunAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), true, Ct)
            );

            Assert.StartsWith("ACCESS_DENIED:", error.Message);
        }

        [Fact]
        public async Task StartRunWithWritePermissionIsAllowed()
        {
            var id = Guid.NewGuid();
            var launcher = new FakeLauncher { Next = Result.Ok(id) };
            var store = new FakeRunStore(Run(id));
            var admin = new FakeCaller { Permissions = ["runs.write"] };
            var tools = Tools(launcher, store, new FakeRuns(), new FakeUnitOfWork(), admin);

            var response = await tools.StartRunAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                true,
                Ct
            );

            Assert.Equal(id, response.Id);
        }

        [Fact]
        public void RejectsNulls()
        {
            var launcher = new FakeLauncher();
            var store = new FakeRunStore();
            var start = new StartRunHandler(launcher, store, Consent, CallerContext.LocalOperator);
            var cancel = new CancelRunHandler(
                launcher,
                new FakeRuns(),
                new FakeUnitOfWork(),
                store,
                new FixedClock(Now),
                CallerContext.LocalOperator
            );
            var caller = new FakeCaller();
            Assert.Throws<ArgumentNullException>(() => new RunActionsTools(null!, cancel, caller));
            Assert.Throws<ArgumentNullException>(() => new RunActionsTools(start, null!, caller));
            Assert.Throws<ArgumentNullException>(() => new RunActionsTools(start, cancel, null!));
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];

            public IReadOnlyCollection<string> Permissions { get; init; } =
            [
                "profiles.read",
                "profiles.write",
                "runs.read",
                "runs.write",
                "sessions.read",
                "sources.read",
            ];
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return now;
            }
        }

        private sealed class FakeLauncher : IRunLauncher
        {
            public Result<Guid> Next { get; init; } = Result.Ok(Guid.NewGuid());

            public Guid? Executing { get; init; }

            public List<RunScrapeRequest> Started { get; } = [];

            public List<Guid> Cancelled { get; } = [];

            public Task<Result<Guid>> StartAsync(
                RunScrapeRequest request,
                CancellationToken cancellationToken
            )
            {
                Started.Add(request);
                return Task.FromResult(Next);
            }

            public bool TryCancel(Guid runId)
            {
                if (Executing != runId)
                {
                    return false;
                }

                Cancelled.Add(runId);
                return true;
            }
        }

        private sealed class FakeRuns : IScrapeRunRepository
        {
            public Task<ScrapeRun?> GetAsync(ScrapeRunId id, CancellationToken cancellationToken)
            {
                return Task.FromResult<ScrapeRun?>(null);
            }

            public Task AddAsync(ScrapeRun run, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<ScrapeRun>> ListExecutingAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<IReadOnlyList<ScrapeRun>>([]);
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Saves { get; private set; }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                Saves++;
                return Task.FromResult(0);
            }
        }

        private sealed class FakeRunStore(params RunResponse[] runs) : IRunStore
        {
            public Task<RunResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
            {
                return Task.FromResult(runs.FirstOrDefault(r => r.Id == id));
            }

            public Task<RunListResponse> ListAsync(
                int offset,
                int limit,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(
                    new RunListResponse(runs.Skip(offset).Take(limit).ToArray(), runs.Length)
                );
            }

            public Task<RunPostingListResponse?> ListPostingsAsync(
                Guid runId,
                int offset,
                int limit,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<RunPostingListResponse?>(null);
            }

            public Task<IReadOnlyList<ExportPosting>?> ListExportPostingsAsync(
                Guid runId,
                int limit,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<IReadOnlyList<ExportPosting>?>(null);
            }
        }
    }
}
