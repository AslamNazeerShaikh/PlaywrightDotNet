using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Results;
using JobScrape.Application.Tests.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class SessionHandlersTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static (CreateSessionHandler Handler, ScrapeHarness.InMemoryStore Store) Create()
        {
            var store = new ScrapeHarness.InMemoryStore();
            return (new CreateSessionHandler(store, store, new FakeTimeProvider(Now)), store);
        }

        private static IReadOnlyDictionary<string, string[]> FieldErrors(
            Result<SessionResponse> result
        )
        {
            Assert.True(result.IsFailure);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            return Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
        }

        [Fact]
        public async Task CreatesANewChromiumBundledProfileByDefault()
        {
            var (handler, store) = Create();

            var result = await handler.HandleAsync(
                new CreateSessionRequest(" naukri-main ", "NAUKRI"),
                Ct
            );

            Assert.True(result.IsSuccess);
            var session = result.Value;
            Assert.Equal("naukri-main", session.Name);
            Assert.Equal("naukri", session.SourceKey);
            Assert.Equal("Chromium", session.Engine);
            Assert.Equal("Bundled", session.Channel);
            Assert.Equal("New", session.State);
            Assert.Equal("naukri/naukri-main", session.UserDataDirectory);
            Assert.True(session.CanLaunch);
            Assert.Null(session.CooldownUntil);
            Assert.Equal(0, session.ConsecutiveChallenges);
            Assert.Single(store.BrowserProfiles);
            Assert.Equal(1, store.Saves);
        }

        [Theory]
        [InlineData("firefox", null, "Firefox", "Bundled")]
        [InlineData("WEBKIT", "bundled", "WebKit", "Bundled")]
        [InlineData("chromium", "Edge", "Chromium", "Edge")]
        [InlineData(null, "chrome", "Chromium", "Chrome")]
        public async Task AcceptsValidEngineAndChannelPairs(
            string? engine,
            string? channel,
            string expectedEngine,
            string expectedChannel
        )
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(
                new CreateSessionRequest("p", "naukri", engine, channel),
                Ct
            );

            Assert.Equal(expectedEngine, result.Value.Engine);
            Assert.Equal(expectedChannel, result.Value.Channel);
        }

        [Fact]
        public async Task ReportsEveryInvalidFieldAndSavesNothing()
        {
            var (handler, store) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(
                    new CreateSessionRequest("../escape", "monster", "netscape", "opera"),
                    Ct
                )
            );

            Assert.Equal(["channel", "engine", "name", "sourceKey"], errors.Keys.Order());
            Assert.Empty(store.BrowserProfiles);
            Assert.Equal(0, store.Saves);
        }

        [Theory]
        [InlineData("1", null, "engine")]
        [InlineData("-1", null, "engine")]
        [InlineData("Chromium,Firefox", null, "engine")]
        [InlineData(null, "2", "channel")]
        [InlineData("Firefox", "Chrome", "channel")]
        [InlineData("WebKit", "Edge", "channel")]
        public async Task RejectsNumericUndefinedAndInvalidPairings(
            string? engine,
            string? channel,
            string field
        )
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(
                    new CreateSessionRequest("p", "naukri", engine, channel),
                    Ct
                )
            );

            Assert.Equal([field], errors.Keys);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData(".hidden")]
        public async Task RejectsUnsafeNames(string? name)
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(new CreateSessionRequest(name, "naukri"), Ct)
            );

            Assert.Equal(["name"], errors.Keys);
        }

        [Fact]
        public async Task DuplicateNameIsAConflict()
        {
            var (handler, store) = Create();
            await handler.HandleAsync(new CreateSessionRequest("main", "naukri"), Ct);

            var result = await handler.HandleAsync(new CreateSessionRequest("main", "indeed"), Ct);

            Assert.Equal("SESSION_NAME_TAKEN", result.Error.Code);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Single(store.BrowserProfiles);
        }

        [Fact]
        public async Task ListAndGetReadThroughTheStore()
        {
            var profile = BrowserProfile.Create(
                "main",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            var session = SessionResponse.From(profile, Now);
            var store = new OneSessionStore(session);

            var list = await new ListSessionsHandler(store).HandleAsync(Ct);
            var hit = await new GetSessionHandler(store).HandleAsync(session.Id, Ct);
            var miss = await new GetSessionHandler(new OneSessionStore(null)).HandleAsync(
                Guid.NewGuid(),
                Ct
            );
            var empty = await new GetSessionHandler(store).HandleAsync(Guid.Empty, Ct);

            Assert.Equal(1, list.Total);
            Assert.Same(session, hit.Value);
            Assert.Equal("SESSION_NOT_FOUND", miss.Error.Code);
            Assert.Equal("VALIDATION_FAILED", empty.Error.Code);
        }

        [Fact]
        public void ReadModelReflectsCooldown()
        {
            var profile = BrowserProfile.Create(
                "main",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            profile.MarkVerified(Now);
            profile.MarkChallenged(ChallengeKind.Captcha, Now, TimeSpan.FromHours(1));

            var during = SessionResponse.From(profile, Now.AddMinutes(30));
            var after = SessionResponse.From(profile, Now.AddHours(1));

            Assert.Equal("CoolingDown", during.State);
            Assert.False(during.CanLaunch);
            Assert.Equal(Now.AddHours(1), during.CooldownUntil);
            Assert.Equal(1, during.ConsecutiveChallenges);
            Assert.Equal(Now, during.LastVerifiedAt);
            Assert.True(after.CanLaunch);
        }

        [Fact]
        public async Task GuardsRejectNulls()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var clock = new FakeTimeProvider();

            Assert.Throws<ArgumentNullException>(() =>
                new CreateSessionHandler(null!, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CreateSessionHandler(store, null!, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CreateSessionHandler(store, store, null!)
            );
            Assert.Throws<ArgumentNullException>(() => new ListSessionsHandler(null!));
            Assert.Throws<ArgumentNullException>(() => new GetSessionHandler(null!));
            Assert.Throws<ArgumentNullException>(() => SessionResponse.From(null!, Now));
            Assert.Throws<ArgumentNullException>(() => Errors.SessionNameTaken(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Create().Handler.HandleAsync(null!, Ct)
            );
        }

        private sealed class OneSessionStore(SessionResponse? session) : ISessionStore
        {
            public Task<SessionResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
            {
                return Task.FromResult(session);
            }

            public Task<IReadOnlyList<SessionResponse>> ListAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<IReadOnlyList<SessionResponse>>(
                    session is null ? [] : [session]
                );
            }
        }
    }
}
