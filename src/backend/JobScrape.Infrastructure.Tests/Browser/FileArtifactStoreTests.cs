using System.Globalization;
using System.Text;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Browser
{
    /// <summary>
    /// Artifact capture without a browser: the port fakes record the paths
    /// and payloads, so the test never launches Chromium.
    /// </summary>
    public sealed class FileArtifactStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task CaptureWithoutAnOpenPageWritesAssessmentOnly()
        {
            var files = new RecordingFileSystem();
            var store = Store(files);
            var context = Context();

            var relative = await store.CaptureAsync(
                context,
                new ChallengeAssessment(ChallengeKind.Captcha, 0.8, " status=403 "),
                Ct
            );

            Assert.Equal(
                $"runs/{context.RunId.Value}/{Now.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}-Captcha",
                relative
            );
            var directory = files.Combine("/artifacts", relative);
            Assert.Contains(directory, files.CreatedDirectories);
            Assert.True(
                files.Writes.TryGetValue(
                    files.Combine(directory, "assessment.json"),
                    out var assessment
                ),
                "assessment.json must be written for every capture."
            );
            Assert.Contains("\"kind\":\"Captcha\"", assessment!, StringComparison.Ordinal);
            Assert.Contains("\"confidence\":0.8", assessment!, StringComparison.Ordinal);
            Assert.Contains("\"evidence\":\"status=403\"", assessment!, StringComparison.Ordinal);
            Assert.Contains("\"url\":null", assessment!, StringComparison.Ordinal);
            Assert.DoesNotContain(files.Combine(directory, "page.html"), files.Writes.Keys);
        }

        [Fact]
        public async Task CaptureRejectsMissingContextOrAssessment()
        {
            var store = Store(new RecordingFileSystem());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                store.CaptureAsync(null!, ChallengeAssessment.Clear, Ct)
            );
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                store.CaptureAsync(Context(), null!, Ct)
            );
        }

        [Fact]
        public async Task CaptureStaysBestEffortWhenTheDirectoryCannotBeCreated()
        {
            var files = new RecordingFileSystem { FailDirectoryCreation = true };
            var store = Store(files);
            var context = Context();

            var relative = await store.CaptureAsync(
                context,
                new ChallengeAssessment(ChallengeKind.Captcha, 0.8, " status=403 "),
                Ct
            );

            Assert.Equal(
                $"runs/{context.RunId.Value}/{Now.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}-Captcha",
                relative
            );
        }

        [Fact]
        public async Task CaptureStaysBestEffortWhenTheAssessmentCannotBeWritten()
        {
            var files = new RecordingFileSystem { FailWrites = true };
            var store = Store(files);
            var context = Context();

            var relative = await store.CaptureAsync(
                context,
                new ChallengeAssessment(ChallengeKind.Captcha, 0.8, " status=403 "),
                Ct
            );

            Assert.NotEmpty(relative);
        }

        [Fact]
        public void ConstructorRejectsNullCollaborators()
        {
            var files = new RecordingFileSystem();
            var settings = Settings();
            var clock = new FakeTimeProvider(Now);
            var logger = NullLogger<FileArtifactStore>.Instance;

            Assert.Throws<ArgumentNullException>(() =>
                new FileArtifactStore(null!, settings, clock, logger, files)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new FileArtifactStore(new NoPagePool(), null!, clock, logger, files)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new FileArtifactStore(new NoPagePool(), settings, null!, logger, files)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new FileArtifactStore(new NoPagePool(), settings, clock, null!, files)
            );
        }

        private static FileArtifactStore Store(RecordingFileSystem files)
        {
            return new FileArtifactStore(
                new NoPagePool(),
                Settings(),
                new FakeTimeProvider(Now),
                NullLogger<FileArtifactStore>.Instance,
                files
            );
        }

        private static BrowserSessionSettings Settings()
        {
            return new BrowserSessionSettings(
                "/profiles",
                "/artifacts",
                Headless: true,
                TimeSpan.FromSeconds(10),
                AdblockMode: null,
                ExtensionDir: null
            );
        }

        private static SourceRunContext Context()
        {
            return new SourceRunContext(
                ScrapeRunId.New(),
                SourceKey.Naukri,
                BrowserProfileId.New(),
                new SearchCriteria("dot net developer", ["Pune"], 5, 7, false, [])
            );
        }

        private sealed class NoPagePool : IBrowserSessionPool
        {
            public Task<IPage> OpenAsync(
                BrowserProfile profile,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException("The artifact store never opens pages.");
            }

            public IPage? Find(BrowserProfileId profileId)
            {
                return null;
            }

            public Task ReleaseAsync(BrowserProfileId profileId)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingFileSystem : IFileSystem
        {
            public char DirectorySeparatorChar => '/';

            public bool FailDirectoryCreation { get; init; }

            public bool FailWrites { get; init; }

            public List<string> CreatedDirectories { get; } = [];

            public Dictionary<string, string> Writes { get; } = new(StringComparer.Ordinal);

            public string Combine(params string[] paths)
            {
                return string.Join('/', paths);
            }

            public void CreateDirectory(string path)
            {
                if (FailDirectoryCreation)
                {
                    throw new IOException("disk full");
                }

                CreatedDirectories.Add(path);
            }

            public bool DirectoryExists(string? path)
            {
                return path is not null && CreatedDirectories.Contains(path);
            }

            public void MoveDirectory(string source, string destination)
            {
                throw new NotSupportedException("The artifact store never moves directories.");
            }

            public Task WriteAllTextAsync(
                string path,
                string contents,
                Encoding encoding,
                CancellationToken cancellationToken
            )
            {
                if (FailWrites)
                {
                    throw new UnauthorizedAccessException("read-only");
                }

                Writes.Add(path, contents);
                return Task.CompletedTask;
            }
        }
    }
}
