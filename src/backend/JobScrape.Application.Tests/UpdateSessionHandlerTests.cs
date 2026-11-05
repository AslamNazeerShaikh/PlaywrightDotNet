using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Sessions;
using JobScrape.Application.Results;
using JobScrape.Application.Tests.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class UpdateSessionHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static (
            UpdateSessionHandler Handler,
            ScrapeHarness.InMemoryStore Store,
            FakeWorkspace Workspace
        ) Create(string name = "before")
        {
            var store = new ScrapeHarness.InMemoryStore();
            var profile = BrowserProfile.Create(
                name,
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            store.BrowserProfiles.Add(profile);
            var workspace = new FakeWorkspace();
            return (
                new UpdateSessionHandler(
                    store,
                    workspace,
                    store,
                    new FakeTimeProvider(Now),
                    NullLogger<UpdateSessionHandler>.Instance
                ),
                store,
                workspace
            );
        }

        [Fact]
        public async Task RenamesTheProfileAndMovesTheDirectory()
        {
            var (handler, store, workspace) = Create();

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest("after", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal("after", result.Value.Name);
            Assert.Equal("naukri/after", result.Value.UserDataDirectory);
            Assert.Equal([("before", "after")], workspace.Moves);
            Assert.Equal(1, store.Saves);
        }

        [Theory]
        [InlineData(ProfileDirectoryRename.ProfileInUse)]
        [InlineData(ProfileDirectoryRename.TargetExists)]
        [InlineData(ProfileDirectoryRename.Failed)]
        public async Task BlockedDirectoryMovesReturnAConflict(ProfileDirectoryRename outcome)
        {
            var (handler, store, workspace) = Create();
            workspace.Outcomes.Enqueue(outcome);

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest("after", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("SESSION_RENAME_BLOCKED", result.Error.Code);
            Assert.Equal(0, store.Saves);
            Assert.Equal("before", store.BrowserProfiles[0].Name);
        }

        [Fact]
        public async Task SaveFailureAfterAMoveMovesTheDirectoryBack()
        {
            var (handler, store, workspace) = Create();
            store.FailAtSave = 1;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(
                    store.BrowserProfiles[0].Id.Value,
                    new UpdateSessionRequest("after", "Chromium", "Bundled"),
                    Ct
                )
            );

            Assert.Equal([("before", "after"), ("after", "before")], workspace.Moves);
            Assert.Equal("before", store.BrowserProfiles[0].Name);
        }

        [Fact]
        public async Task ChangesEngineAndChannelWithoutMoving()
        {
            var (handler, store, workspace) = Create();

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest(null, "Chromium", "Chrome"),
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal("Chrome", result.Value.Channel);
            Assert.Empty(workspace.Moves);
        }

        [Fact]
        public async Task DuplicateNameIsAConflict()
        {
            var (handler, store, _) = Create();
            store.BrowserProfiles.Add(
                BrowserProfile.Create(
                    "taken",
                    SourceKey.Naukri,
                    BrowserEngine.Chromium,
                    BrowserChannel.Bundled
                )
            );

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest("taken", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("SESSION_NAME_TAKEN", result.Error.Code);
            Assert.Equal(0, store.Saves);
        }

        [Fact]
        public async Task CaseVariantOfAnotherNameIsAConflict()
        {
            var (handler, store, _) = Create();
            store.BrowserProfiles.Add(
                BrowserProfile.Create(
                    "taken",
                    SourceKey.Naukri,
                    BrowserEngine.Chromium,
                    BrowserChannel.Bundled
                )
            );

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest("TAKEN", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("SESSION_NAME_TAKEN", result.Error.Code);
        }

        [Fact]
        public async Task CaseOnlySelfRenameIsAllowed()
        {
            var (handler, store, _) = Create();

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest("Before", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal("Before", result.Value.Name);
        }

        [Fact]
        public async Task InvalidPairingReturnsFieldErrors()
        {
            var (handler, store, _) = Create();

            var result = await handler.HandleAsync(
                store.BrowserProfiles[0].Id.Value,
                new UpdateSessionRequest(null, "Firefox", "Chrome"),
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
            Assert.Contains("channel", errors.Keys);
        }

        [Fact]
        public async Task UnknownProfileIsNotFound()
        {
            var (handler, _, _) = Create();

            var result = await handler.HandleAsync(
                Guid.NewGuid(),
                new UpdateSessionRequest("after", "Chromium", "Bundled"),
                Ct
            );

            Assert.True(result.IsFailure);
            Assert.Equal("SESSION_NOT_FOUND", result.Error.Code);
        }

        private sealed class FakeWorkspace : IBrowserProfileWorkspace
        {
            public Queue<ProfileDirectoryRename> Outcomes { get; } = new();

            public List<(string From, string To)> Moves { get; } = [];

            public Task<ProfileDirectoryRename> RenameAsync(
                BrowserProfile profile,
                string newName,
                CancellationToken cancellationToken
            )
            {
                Moves.Add((profile.Name, newName));
                return Task.FromResult(
                    Outcomes.Count > 0 ? Outcomes.Dequeue() : ProfileDirectoryRename.Moved
                );
            }
        }
    }
}
