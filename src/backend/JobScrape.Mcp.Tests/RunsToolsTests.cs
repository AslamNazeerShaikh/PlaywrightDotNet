using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class RunsToolsTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

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

        private static RunsTools Tools(FakeRunStore store)
        {
            return new RunsTools(
                new ListRunsHandler(store),
                new GetRunHandler(store),
                new ListRunPostingsHandler(store),
                new FakeCaller()
            );
        }

        [Fact]
        public async Task ListRunsReturnsNewestFirst()
        {
            var runs = new[] { Run(), Run() };
            var store = new FakeRunStore(runs);

            var response = await Tools(store).ListRunsAsync(0, 50, Ct);

            Assert.Equal(2, response.Total);
            Assert.Equal(runs.Select(r => r.Id), response.Items.Select(r => r.Id));
            Assert.Equal((0, 50), (store.LastOffset, store.LastLimit));
        }

        [Fact]
        public async Task GetRunReturnsRun()
        {
            var run = Run();
            var response = await Tools(new FakeRunStore(run)).GetRunAsync(run.Id.ToString(), Ct);

            Assert.Equal(run, response);
        }

        [Fact]
        public async Task GetRunBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeRunStore()).GetRunAsync("not-a-guid", Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetRunEmptyIdFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeRunStore()).GetRunAsync(Guid.Empty.ToString(), Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetRunMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeRunStore()).GetRunAsync(Guid.NewGuid().ToString(), Ct)
            );

            Assert.StartsWith("RUN_NOT_FOUND:", error.Message);
        }

        [Fact]
        public async Task ListRunPostingsReturnsPostings()
        {
            var run = Run();
            var store = new FakeRunStore(run);

            var response = await Tools(store).ListRunPostingsAsync(run.Id.ToString(), 0, 200, Ct);

            Assert.Equal(run.Id, response.RunId);
            Assert.Single(response.Items);
            Assert.Equal((0, 200), (store.LastOffset, store.LastLimit));
        }

        [Fact]
        public async Task ListRunPostingsBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeRunStore()).ListRunPostingsAsync("not-a-guid", 0, 200, Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task ListRunPostingsMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeRunStore())
                    .ListRunPostingsAsync(Guid.NewGuid().ToString(), 0, 200, Ct)
            );

            Assert.StartsWith("RUN_NOT_FOUND:", error.Message);
        }

        [Fact]
        public void RejectsNulls()
        {
            var store = new FakeRunStore();
            var caller = new FakeCaller();
            var list = new ListRunsHandler(store);
            var get = new GetRunHandler(store);
            var postings = new ListRunPostingsHandler(store);
            Assert.Throws<ArgumentNullException>(() => new RunsTools(null!, get, postings, caller));
            Assert.Throws<ArgumentNullException>(() =>
                new RunsTools(list, null!, postings, caller)
            );
            Assert.Throws<ArgumentNullException>(() => new RunsTools(list, get, null!, caller));
            Assert.Throws<ArgumentNullException>(() => new RunsTools(list, get, postings, null!));
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];
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
                return Task.FromResult<IReadOnlyList<ExportPosting>?>(null);
            }
        }
    }
}
