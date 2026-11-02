using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class RunScrapeOnSqliteTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task TwoRunsPersistDedupeAndChargeTheDailyQuota()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var clock = new FakeTimeProvider(Now);
            var profile = SearchProfile.Create(
                "backend",
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], null, null, remoteOnly: false, []),
                new RunLimits(3, 60, 0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)),
                Now
            );
            var browser = BrowserProfile.Create(
                "main",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            browser.MarkVerified(Now);
            await using (var scope = database.Scope())
            {
                scope.Db.SearchProfiles.Add(profile);
                scope.Db.BrowserProfiles.Add(browser);
                await scope.Db.SaveChangesAsync(Ct);
            }

            var source = new TwoPageSource();
            var request = new RunScrapeRequest(profile.Id, browser.Id, ComplianceConsent.None);

            async Task<Result<ScrapeRunSummary>> RunAsync()
            {
                await using var scope = database.Scope();
                var handler = new RunScrapeHandler(
                    scope.Get<ScrapeStores>(),
                    new JobSourceRegistry([source]),
                    new SourceSettingsCatalog([
                        new SourceSettings(
                            SourceKey.Naukri,
                            true,
                            25,
                            60,
                            2,
                            TimeSpan.FromMinutes(45),
                            profile.Limits
                        ),
                    ]),
                    new ReadySessions(),
                    new NoArtifacts(),
                    new NoDelay(),
                    new RetryPolicy(2, new MinRandom()),
                    new MinRandom(),
                    clock,
                    NullLogger<RunScrapeHandler>.Instance
                );
                return await handler.HandleAsync(request, Ct);
            }

            var first = (await RunAsync()).Value;
            clock.Advance(TimeSpan.FromHours(1));
            var second = (await RunAsync()).Value;

            Assert.Equal(RunStage.Completed, first.Stage);
            Assert.Equal(3, first.Stats.PostingsNew);
            Assert.Equal(RunStage.Completed, second.Stage);
            Assert.Equal(0, second.Stats.PostingsNew);
            Assert.Equal(3, second.Stats.PostingsFound);

            await using var verify = database.Scope();
            var postings = await verify.Db.JobPostings.ToListAsync(Ct);
            Assert.Equal(3, postings.Count);
            Assert.All(postings, p => Assert.Equal(Now, p.FirstSeenAt));
            Assert.All(postings, p => Assert.Equal(Now.AddHours(1), p.LastSeenAt));
            var runs = await verify.Db.ScrapeRuns.ToListAsync(Ct);
            Assert.Equal(2, runs.Count);
            Assert.All(runs, run => Assert.Equal(RunStage.Completed, run.Stage));
            var quota = await verify.Db.SourceQuotas.SingleAsync(Ct);
            Assert.Equal(2, quota.RunsStarted);
            Assert.Equal(4, quota.PagesFetched);
            Assert.Equal(
                CircuitState.Closed,
                (await verify.Db.SourceCircuitBreakers.SingleAsync(Ct)).State
            );
            Assert.Equal(
                Now.AddHours(1),
                (await verify.Db.BrowserProfiles.SingleAsync(Ct)).LastUsedAt
            );

            // The daily run cap is 2; the count is read back from SQLite.
            var third = await RunAsync();
            Assert.Equal("QUOTA_EXCEEDED", third.Error.Code);
        }

        private sealed class TwoPageSource : IJobSource
        {
            public SourceKey Key => SourceKey.Naukri;

            public SourceCapabilities Capabilities { get; } = new(SupportsDetails: false);

            public Task<SearchPageResult> SearchPageAsync(
                SourceRunContext context,
                SearchPageRequest request,
                CancellationToken cancellationToken
            )
            {
                var ids = request.PageNumber == 1 ? new[] { "101", "102" } : ["103"];
                var jobs = ids.Select(id => new JobSnapshot
                    {
                        Source = SourceKey.Naukri,
                        ExternalJobId = id,
                        Url = $"https://www.naukri.com/job-listings-x-{id}",
                        Title = $"Engineer {id}",
                    })
                    .ToArray();
                return Task.FromResult(
                    new SearchPageResult(
                        jobs,
                        request.PageNumber == 1 ? "p2" : null,
                        new PageFingerprint(ids[0], null),
                        ChallengeAssessment.Clear
                    )
                );
            }

            public Task<JobDetailResult> GetDetailAsync(
                SourceRunContext context,
                JobReference job,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ReadySessions : ISessionManager
        {
            public Task<ChallengeAssessment> EnsureSessionAsync(
                SourceKey source,
                BrowserProfileId profileId,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(ChallengeAssessment.Clear);
            }
        }

        private sealed class NoArtifacts : IArtifactStore
        {
            public Task<string> CaptureAsync(
                SourceRunContext context,
                ChallengeAssessment assessment,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException();
            }
        }

        private sealed class NoDelay : IDelay
        {
            public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class MinRandom : IRandomSource
        {
            public int NextInt32(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
