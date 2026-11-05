using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Browser;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Browser
{
    [Trait("Category", "Browser")]
    public sealed class BrowserProfileRenameTests : IAsyncLifetime
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-rename-{Guid.NewGuid():N}"
        );

        private readonly BrowserProfile _profile = BrowserProfile.Create(
            "before",
            SourceKey.Naukri,
            BrowserEngine.Chromium,
            BrowserChannel.Bundled
        );

        private BrowserSessionPool _pool = null!;

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public ValueTask InitializeAsync()
        {
            _pool = new BrowserSessionPool(
                new BrowserSessionSettings(
                    Path.Combine(_root, "profiles"),
                    Path.Combine(_root, "artifacts"),
                    Headless: true,
                    TimeSpan.FromSeconds(10),
                    AdblockMode: null,
                    ExtensionDir: null
                )
            );
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _pool.DisposeAsync();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private string DirectoryFor(string name)
        {
            return Path.Combine(_root, "profiles", "naukri", name);
        }

        [Fact]
        public async Task MovesAnExistingDirectory()
        {
            var oldDirectory = DirectoryFor("before");
            Directory.CreateDirectory(oldDirectory);
            await File.WriteAllTextAsync(Path.Combine(oldDirectory, "marker.txt"), "kept", Ct);

            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.Moved, outcome);
            Assert.False(Directory.Exists(oldDirectory));
            Assert.True(File.Exists(Path.Combine(DirectoryFor("after"), "marker.txt")));
        }

        [Fact]
        public async Task MissingDirectoriesAreMetadataOnly()
        {
            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.NothingToMove, outcome);
        }

        [Fact]
        public async Task AlreadyMovedDirectoryIsIdempotent()
        {
            Directory.CreateDirectory(DirectoryFor("after"));

            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.AlreadyRenamed, outcome);
        }

        [Fact]
        public async Task ExistingTargetIsRefused()
        {
            Directory.CreateDirectory(DirectoryFor("before"));
            Directory.CreateDirectory(DirectoryFor("after"));

            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.TargetExists, outcome);
        }

        [Fact]
        public async Task CaseOnlyRenameUsesATemporaryName()
        {
            Directory.CreateDirectory(DirectoryFor("before"));

            var outcome = await _pool.RenameAsync(_profile, "Before", Ct);

            Assert.Equal(ProfileDirectoryRename.Moved, outcome);
            Assert.True(Directory.Exists(DirectoryFor("Before")));
            Assert.False(
                Directory.Exists(DirectoryFor($"Before.{_profile.Id.Value:N}.rename-tmp"))
            );
        }

        [Fact]
        public async Task ResumesADeterministicTemporaryDirectory()
        {
            var temporary = DirectoryFor($"after.{_profile.Id.Value:N}.rename-tmp");
            Directory.CreateDirectory(temporary);
            await File.WriteAllTextAsync(Path.Combine(temporary, "marker.txt"), "kept", Ct);

            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.Moved, outcome);
            Assert.True(File.Exists(Path.Combine(DirectoryFor("after"), "marker.txt")));
        }

        [Fact]
        public async Task OpenContextBlocksTheRename()
        {
            await _pool.OpenAsync(_profile, Ct);

            var outcome = await _pool.RenameAsync(_profile, "after", Ct);

            Assert.Equal(ProfileDirectoryRename.ProfileInUse, outcome);
            await _pool.ReleaseAsync(_profile.Id);
        }
    }
}
