using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Application.Tests.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class RunHandlersTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly ComplianceConsent Consent = new(true, false, false);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static ScrapeRun Run(RunStage stage)
        {
            var run = ScrapeRun.Create(
                SearchProfile.Create(
                    "backend",
                    SourceKey.Naukri,
                    new SearchCriteria("c#", ["Pune"], null, null, false, []),
                    new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                    Now
                ),
                Now
            );
            run.Start(Now);
            if (stage == RunStage.PausedForChallenge)
            {
                run.BeginSearching();
                run.PauseForChallenge(ChallengeKind.Captcha, Now);
            }
            else if (stage == RunStage.Failed)
            {
                run.Fail("boom", Now);
            }

            return run;
        }

        [Fact]
        public void ConstructorsRejectNulls()
        {
            var store = new FakeRunStore();
            var launcher = new FakeLauncher();
            var memory = new ScrapeHarness.InMemoryStore();
            var clock = new FakeTimeProvider(Now);
            Assert.Throws<ArgumentNullException>(() => new StartRunHandler(null!, store, Consent));
            Assert.Throws<ArgumentNullException>(() =>
                new StartRunHandler(launcher, null!, Consent)
            );
            Assert.Throws<ArgumentNullException>(() => new StartRunHandler(launcher, store, null!));
            Assert.Throws<ArgumentNullException>(() => new ListRunsHandler(null!));
            Assert.Throws<ArgumentNullException>(() => new GetRunHandler(null!));
            Assert.Throws<ArgumentNullException>(() => new ListRunPostingsHandler(null!));
            Assert.Throws<ArgumentNullException>(() =>
                new CancelRunHandler(null!, memory, memory, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CancelRunHandler(launcher, null!, memory, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CancelRunHandler(launcher, memory, null!, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CancelRunHandler(launcher, memory, memory, null!, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CancelRunHandler(launcher, memory, memory, store, null!)
            );
        }

        [Fact]
        public async Task StartPassesTheConfiguredConsentAndReturnsTheRecordedRun()
        {
            var run = Run(RunStage.StartingSession);
            var store = new FakeRunStore(RunResponse.From(run, "backend"));
            var launcher = new FakeLauncher { Next = Result.Ok(run.Id.Value) };
            var profileId = Guid.NewGuid();
            var browserId = Guid.NewGuid();

            var result = await new StartRunHandler(launcher, store, Consent).HandleAsync(
                new StartRunRequest(profileId, browserId, Enrich: false),
                Ct
            );

            Assert.Equal(run.Id.Value, result.Value.Id);
            var sent = Assert.Single(launcher.Started);
            Assert.Equal(profileId, sent.ProfileId.Value);
            Assert.Equal(browserId, sent.BrowserProfileId.Value);
            Assert.Same(Consent, sent.Consent);
            Assert.False(sent.Enrich);
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                new StartRunHandler(launcher, store, Consent).HandleAsync(null!, Ct)
            );
        }

        [Fact]
        public async Task StartRejectsEmptyIdsBeforeLaunching()
        {
            var launcher = new FakeLauncher();
            var result = await new StartRunHandler(
                launcher,
                new FakeRunStore(),
                Consent
            ).HandleAsync(new StartRunRequest(Guid.Empty, Guid.Empty), Ct);

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
            Assert.Equal(["browserProfileId", "profileId"], errors.Keys.Order());
            Assert.Empty(launcher.Started);
        }

        [Fact]
        public async Task StartReportsGateRefusalsAndAVanishedRun()
        {
            var refused = new FakeLauncher { Next = Result.Fail<Guid>(Errors.RunInProgress(null)) };
            var request = new StartRunRequest(Guid.NewGuid(), Guid.NewGuid());
            var conflict = await new StartRunHandler(
                refused,
                new FakeRunStore(),
                Consent
            ).HandleAsync(request, Ct);
            Assert.Equal("RUN_IN_PROGRESS", conflict.Error.Code);

            var ghost = new FakeLauncher { Next = Result.Ok(Guid.NewGuid()) };
            var missing = await new StartRunHandler(ghost, new FakeRunStore(), Consent).HandleAsync(
                request,
                Ct
            );
            Assert.Equal("RUN_NOT_FOUND", missing.Error.Code);
        }

        [Fact]
        public async Task GetListAndPostingsValidateAndReportMissingRuns()
        {
            var run = RunResponse.From(Run(RunStage.StartingSession), null);
            var store = new FakeRunStore(run);

            Assert.Equal(run, (await new GetRunHandler(store).HandleAsync(run.Id, Ct)).Value);
            Assert.Equal(
                "RUN_NOT_FOUND",
                (await new GetRunHandler(store).HandleAsync(Guid.NewGuid(), Ct)).Error.Code
            );
            Assert.Equal(
                "VALIDATION_FAILED",
                (await new GetRunHandler(store).HandleAsync(Guid.Empty, Ct)).Error.Code
            );

            var list = await new ListRunsHandler(store).HandleAsync(0, ListRunsHandler.Limit, Ct);
            Assert.Equal(ListRunsHandler.Limit, store.LastLimit);
            Assert.Equal(0, store.LastOffset);
            Assert.Single(list.Items);

            var postings = new ListRunPostingsHandler(store);
            var listed = Assert.Single(
                (await postings.HandleAsync(run.Id, 0, ListRunPostingsHandler.Limit, Ct)).Value.Items
            );
            Assert.Equal("Developer", listed.Title);
            Assert.Equal(ListRunPostingsHandler.Limit, store.LastLimit);
            Assert.Equal(0, store.LastOffset);
            Assert.Equal(
                "RUN_NOT_FOUND",
                (await postings.HandleAsync(Guid.NewGuid(), 0, ListRunPostingsHandler.Limit, Ct)).Error.Code
            );
            Assert.Equal(
                "VALIDATION_FAILED",
                (await postings.HandleAsync(Guid.Empty, 0, ListRunPostingsHandler.Limit, Ct)).Error.Code
            );
        }

        [Fact]
        public async Task PagingForwardsOffsetFirstWithAsymmetricValues()
        {
            var runs = new[]
            {
                RunResponse.From(Run(RunStage.StartingSession), null),
                RunResponse.From(Run(RunStage.StartingSession), null),
                RunResponse.From(Run(RunStage.StartingSession), null),
            };
            var store = new FakeRunStore(runs);

            var page = await new ListRunsHandler(store).HandleAsync(1, 2, Ct);
            Assert.Equal([runs[1].Id, runs[2].Id], page.Items.Select(r => r.Id));
            Assert.Equal(1, store.LastOffset);
            Assert.Equal(2, store.LastLimit);

            var postings = await new ListRunPostingsHandler(store).HandleAsync(
                runs[0].Id,
                1,
                2,
                Ct
            );
            Assert.True(postings.IsSuccess);
            Assert.Equal(1, store.LastOffset);
            Assert.Equal(2, store.LastLimit);
        }

        [Fact]
        public async Task PagingClampsNegativeOffsetAndOutOfRangeLimit()
        {
            var run = RunResponse.From(Run(RunStage.StartingSession), null);
            var store = new FakeRunStore(run);

            await new ListRunsHandler(store).HandleAsync(-5, 0, Ct);
            Assert.Equal((0, ListRunsHandler.Limit), (store.LastOffset, store.LastLimit));

            await new ListRunsHandler(store).HandleAsync(0, 9999, Ct);
            Assert.Equal((0, 500), (store.LastOffset, store.LastLimit));

            var postings = await new ListRunPostingsHandler(store).HandleAsync(
                run.Id,
                -3,
                0,
                Ct
            );
            Assert.True(postings.IsSuccess);
            Assert.Equal((0, ListRunPostingsHandler.Limit), (store.LastOffset, store.LastLimit));

            var capped = await new ListRunPostingsHandler(store).HandleAsync(run.Id, 2, 9999, Ct);
            Assert.True(capped.IsSuccess);
            Assert.Equal((2, 500), (store.LastOffset, store.LastLimit));
        }

        [Fact]
        public async Task CancelSignalsAnExecutingRunWithoutTouchingTheStore()
        {
            var run = Run(RunStage.StartingSession);
            var memory = new ScrapeHarness.InMemoryStore();
            var launcher = new FakeLauncher { Executing = run.Id.Value };
            var handler = new CancelRunHandler(
                launcher,
                memory,
                memory,
                new FakeRunStore(RunResponse.From(run, null)),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(run.Id.Value, Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal([run.Id.Value], launcher.Cancelled);
            Assert.Equal(0, memory.Saves);
        }

        [Fact]
        public async Task CancelEndsAPausedRunDirectlyAndLeavesTerminalRunsAlone()
        {
            var paused = Run(RunStage.PausedForChallenge);
            var failed = Run(RunStage.Failed);
            var memory = new ScrapeHarness.InMemoryStore();
            memory.Runs.AddRange([paused, failed]);
            var store = new FakeRunStore(
                RunResponse.From(paused, null),
                RunResponse.From(failed, null)
            );
            var handler = new CancelRunHandler(
                new FakeLauncher(),
                memory,
                memory,
                store,
                new FakeTimeProvider(Now.AddMinutes(1))
            );

            Assert.True((await handler.HandleAsync(paused.Id.Value, Ct)).IsSuccess);
            Assert.Equal(RunStage.Cancelled, paused.Stage);
            Assert.Equal(1, memory.Saves);

            Assert.True((await handler.HandleAsync(failed.Id.Value, Ct)).IsSuccess);
            Assert.Equal(RunStage.Failed, failed.Stage);
            Assert.Equal(1, memory.Saves);

            Assert.Equal(
                "RUN_NOT_FOUND",
                (await handler.HandleAsync(Guid.NewGuid(), Ct)).Error.Code
            );
            Assert.Equal(
                "VALIDATION_FAILED",
                (await handler.HandleAsync(Guid.Empty, Ct)).Error.Code
            );
        }

        [Fact]
        public async Task CancelReportsARunThatVanishesBetweenWriteAndRead()
        {
            var paused = Run(RunStage.PausedForChallenge);
            var memory = new ScrapeHarness.InMemoryStore();
            memory.Runs.Add(paused);
            var handler = new CancelRunHandler(
                new FakeLauncher(),
                memory,
                memory,
                new FakeRunStore(),
                new FakeTimeProvider(Now)
            );

            Assert.Equal(
                "RUN_NOT_FOUND",
                (await handler.HandleAsync(paused.Id.Value, Ct)).Error.Code
            );
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

        private sealed class FakeRunStore(params RunResponse[] runs) : IRunStore
        {
            public int LastLimit { get; private set; }

            public int LastOffset { get; private set; }

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
                LastLimit = limit;
                LastOffset = offset;
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
                LastLimit = limit;
                LastOffset = offset;
                RunPostingResponse posting = new(
                    Guid.NewGuid(),
                    "101",
                    "https://www.naukri.com/job-listings-dev-101",
                    "Developer",
                    null,
                    null,
                    "Unknown",
                    null,
                    null,
                    null,
                    [],
                    false,
                    true,
                    Now,
                    Now
                );
                return Task.FromResult(
                    runs.Any(r => r.Id == runId)
                        ? new RunPostingListResponse(runId, [posting], 1)
                        : null
                );
            }

            public Task<IReadOnlyList<ExportPosting>?> ListExportPostingsAsync(
                Guid runId,
                int limit,
                CancellationToken cancellationToken
            )
            {
                LastLimit = limit;
                ExportPosting posting = new(
                    Guid.NewGuid(),
                    "101",
                    "naukri",
                    "https://www.naukri.com/job-listings-dev-101",
                    null,
                    "Developer",
                    null,
                    null,
                    "Unknown",
                    null,
                    null,
                    null,
                    [],
                    null,
                    Now,
                    Now
                );
                IReadOnlyList<ExportPosting>? items = runs.Any(r => r.Id == runId)
                    ? [posting]
                    : null;
                return Task.FromResult(items);
            }
        }
    }
}
