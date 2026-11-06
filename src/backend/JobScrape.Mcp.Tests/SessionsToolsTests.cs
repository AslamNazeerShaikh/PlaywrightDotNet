using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Sessions;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class SessionsToolsTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SessionResponse Session(Guid? id = null)
        {
            return new SessionResponse(
                id ?? Guid.NewGuid(),
                "main",
                "naukri",
                "Chromium",
                "Bundled",
                "New",
                "naukri/main",
                true,
                null,
                0,
                null,
                null
            );
        }

        private static SessionsTools Tools(StubStore store)
        {
            return new SessionsTools(
                new ListSessionsHandler(store),
                new GetSessionHandler(store),
                new FakeCaller()
            );
        }

        [Fact]
        public async Task ListSessionsReturnsEveryProfile()
        {
            var sessions = new[] { Session(), Session() };

            var response = await Tools(new StubStore(sessions)).ListSessionsAsync(Ct);

            Assert.Equal(2, response.Total);
            Assert.Equal(sessions, response.Items);
        }

        [Fact]
        public async Task GetSessionReturnsProfile()
        {
            var session = Session();

            var response = await Tools(new StubStore([session]))
                .GetSessionAsync(session.Id.ToString(), Ct);

            Assert.Equal(session, response);
        }

        [Fact]
        public async Task GetSessionBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetSessionAsync("not-a-guid", Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetSessionEmptyIdFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetSessionAsync(Guid.Empty.ToString(), Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task GetSessionMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new StubStore([])).GetSessionAsync(Guid.NewGuid().ToString(), Ct)
            );

            Assert.StartsWith("SESSION_NOT_FOUND:", error.Message);
        }

        [Fact]
        public void RejectsNulls()
        {
            var store = new StubStore([]);
            var caller = new FakeCaller();
            var list = new ListSessionsHandler(store);
            var get = new GetSessionHandler(store);
            Assert.Throws<ArgumentNullException>(() => new SessionsTools(null!, get, caller));
            Assert.Throws<ArgumentNullException>(() => new SessionsTools(list, null!, caller));
            Assert.Throws<ArgumentNullException>(() => new SessionsTools(list, get, null!));
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];
        }

        private sealed class StubStore(IReadOnlyList<SessionResponse> sessions) : ISessionStore
        {
            public Task<SessionResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
            {
                return Task.FromResult(sessions.FirstOrDefault(s => s.Id == id));
            }

            public Task<IReadOnlyList<SessionResponse>> ListAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(sessions);
            }
        }
    }
}
