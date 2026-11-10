using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class RunOnlyNewOnSqliteTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfile Profile(string name)
        {
            return SearchProfile.Create(
                name,
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], null, null, false, []),
                new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                Now
            );
        }

        private static JobSnapshot Snapshot(string id)
        {
            return new JobSnapshot
            {
                Source = SourceKey.Naukri,
                ExternalJobId = id,
                Url = $"https://www.naukri.com/job-listings-dev-{id}",
                Title = $"Developer {id}",
                Skills = ["C#"],
            };
        }

        private static JobPosting Posting(string id, DateTimeOffset seen)
        {
            return JobPosting.Create(Snapshot(id), seen);
        }

        private static async Task<Guid> SeedTwoRunsAsync(TestDatabase database, bool onlyNew)
        {
            var profile = Profile("backend");
            var run1 = ScrapeRun.Create(profile, Now);
            run1.Start(Now);
            var run2 = ScrapeRun.Create(profile, Now.AddHours(1), onlyNew);
            run2.Start(Now.AddHours(1));
            var refreshed = Posting("refreshed", Now.AddMinutes(1));
            refreshed.Refresh(Snapshot("refreshed"), Now.AddHours(1).AddMinutes(2));
            var fresh = Posting("fresh", Now.AddHours(1).AddMinutes(3));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                var runs = scope.Get<IScrapeRunRepository>();
                await runs.AddAsync(run1, Ct);
                await runs.AddAsync(run2, Ct);
                var postings = scope.Get<IJobPostingRepository>();
                await postings.AddAsync(refreshed, Ct);
                await postings.AddAsync(fresh, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }
            return run2.Id.Value;
        }

        [Fact]
        public async Task FlagPersistsAndDefaultsFalse()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var plain = ScrapeRun.Create(profile, Now);
            var flagged = ScrapeRun.Create(profile, Now, onlyNew: true);
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                var runs = scope.Get<IScrapeRunRepository>();
                await runs.AddAsync(plain, Ct);
                await runs.AddAsync(flagged, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var reloaded = reload.Get<IScrapeRunRepository>();
            Assert.False((await reloaded.GetAsync(plain.Id, Ct))!.OnlyNew);
            Assert.True((await reloaded.GetAsync(flagged.Id, Ct))!.OnlyNew);
        }

        [Fact]
        public async Task FlaggedListingHoldsOnlyFirstSeenInWindow()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var run2Id = await SeedTwoRunsAsync(database, onlyNew: true);

            await using var reload = database.Scope();
            var store = new RunReadStore(
                reload.Db,
                new FakeTimeProvider(Now.AddHours(1).AddMinutes(5))
            );

            var seen = await store.ListPostingsAsync(run2Id, 0, 50, Ct);
            Assert.Equal(["fresh"], seen!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(1, seen.Total);
        }

        [Fact]
        public async Task UnflaggedListingIsUnchanged()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var run2Id = await SeedTwoRunsAsync(database, onlyNew: false);

            await using var reload = database.Scope();
            var store = new RunReadStore(
                reload.Db,
                new FakeTimeProvider(Now.AddHours(1).AddMinutes(5))
            );

            var seen = await store.ListPostingsAsync(run2Id, 0, 50, Ct);
            Assert.Equal(["fresh", "refreshed"], seen!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, seen.Total);
        }

        [Fact]
        public async Task BoundaryTickCountsAsNew()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var run = ScrapeRun.Create(profile, Now.AddHours(1), onlyNew: true);
            run.Start(Now.AddHours(1));
            var boundary = Posting("boundary", Now.AddHours(1));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(run, Ct);
                await scope.Get<IJobPostingRepository>().AddAsync(boundary, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var store = new RunReadStore(
                reload.Db,
                new FakeTimeProvider(Now.AddHours(1).AddMinutes(5))
            );

            var seen = await store.ListPostingsAsync(run.Id.Value, 0, 50, Ct);
            Assert.Equal(["boundary"], seen!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(1, seen.Total);
            Assert.True(seen.Items[0].IsNew);
        }

        [Fact]
        public void RunResponseCarriesOnlyNew()
        {
            var profile = Profile("backend");
            var flagged = ScrapeRun.Create(profile, Now, onlyNew: true);

            var response = RunResponse.From(flagged, "backend");

            Assert.True(response.OnlyNew);
        }
    }
}
