using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Runtime;
using JobScrape.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Runtime
{
    public sealed class StorageRetentionServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task SweepDeletesExpiredPostingsAndArtifactDirectories()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                scope.Db.JobPostings.Add(Posting("old", Now.AddDays(-400)));
                scope.Db.JobPostings.Add(Posting("fresh", Now));
                await scope.Db.SaveChangesAsync(Ct);
            }

            var files = new SweepFileSystem();
            files.Add("/artifacts/runs/old-run", Now.AddDays(-31));
            files.Add("/artifacts/runs/fresh-run", Now.AddDays(-1));
            var service = new StorageRetentionService(
                database.ScopeFactory,
                Settings(),
                Options.Create(
                    new StorageOptions { RetentionDays = 30, PostingRetentionDays = 180 }
                ),
                files,
                new FakeTimeProvider(Now),
                NullLogger<StorageRetentionService>.Instance
            );

            await service.SweepAsync(Ct);

            await using (var scope = database.Scope())
            {
                var remaining = await scope
                    .Db.JobPostings.Select(posting => posting.ExternalJobId.Value)
                    .ToListAsync(Ct);
                Assert.Equal(["fresh"], remaining);
            }

            Assert.Equal(["/artifacts/runs/old-run"], files.Deleted);
        }

        private static JobPosting Posting(string externalId, DateTimeOffset seenAt)
        {
            return JobPosting.Create(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = externalId,
                    Url = $"https://www.naukri.com/job-listings-x-{externalId}",
                    Title = "Senior .NET Developer",
                },
                seenAt
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

        private sealed class SweepFileSystem : IFileSystem
        {
            private readonly Dictionary<string, DateTime> _directories = new(
                StringComparer.Ordinal
            );

            public List<string> Deleted { get; } = [];

            public char DirectorySeparatorChar => '/';

            public void Add(string path, DateTimeOffset writtenAt)
            {
                _directories[path] = writtenAt.UtcDateTime;
            }

            public string Combine(params string[] paths) => string.Join('/', paths);

            public void CreateDirectory(string path)
            {
                throw new NotSupportedException("The sweep never creates directories.");
            }

            public bool DirectoryExists(string? path) =>
                path is not null && _directories.ContainsKey(path);

            public void MoveDirectory(string source, string destination) =>
                throw new NotSupportedException("The sweep never moves directories.");

            public Task WriteAllTextAsync(
                string path,
                string contents,
                System.Text.Encoding encoding,
                CancellationToken cancellationToken
            ) => throw new NotSupportedException("The sweep never writes files.");

            public long GetAvailableFreeBytes(string path) => 0;

            public IReadOnlyList<string> EnumerateDirectories(string path) =>
                _directories
                    .Keys.Where(directory =>
                        string.Equals(
                            System.IO.Path.GetDirectoryName(directory),
                            path,
                            StringComparison.Ordinal
                        )
                    )
                    .Order(StringComparer.Ordinal)
                    .ToList();

            public DateTime GetLastWriteTimeUtc(string path) => _directories[path];

            public void DeleteDirectory(string path, bool recursive)
            {
                _directories.Remove(path);
                Deleted.Add(path);
            }
        }
    }
}
