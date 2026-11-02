using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class RunReadStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfile Profile(string name)
        {
            return SearchProfile.Create(
                name,
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], null, null, false),
                new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                Now
            );
        }

        private static JobPosting Posting(string id, DateTimeOffset seen)
        {
            return JobPosting.Create(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = id,
                    Url = $"https://www.naukri.com/job-listings-dev-{id}",
                    Title = $"Developer {id}",
                    Skills = ["C#"],
                },
                seen
            );
        }

        [Fact]
        public async Task ListsNewestFirstWithProfileNamesAndReadsOne()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var older = ScrapeRun.Create(profile, Now);
            var newer = ScrapeRun.Create(profile, Now.AddMinutes(5));
            newer.Start(Now.AddMinutes(5));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(older, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(newer, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddHours(1)));

            var list = await store.ListAsync(10, Ct);
            Assert.Equal(2, list.Total);
            Assert.Equal([newer.Id.Value, older.Id.Value], list.Items.Select(r => r.Id));
            Assert.All(list.Items, r => Assert.Equal("backend", r.ProfileName));
            Assert.Equal("StartingSession", list.Items[0].Stage);
            Assert.False(list.Items[0].IsTerminal);
            Assert.Equal(3, list.Items[0].MaxPages);

            Assert.Single((await store.ListAsync(1, Ct)).Items);
            Assert.Equal("Pending", (await store.GetAsync(older.Id.Value, Ct))!.Stage);
            Assert.Null(await store.GetAsync(Guid.NewGuid(), Ct));
            Assert.Null(await store.GetAsync(Guid.Empty, Ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListAsync(0, Ct));
            Assert.Empty(reload.Db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task AnEmptyHistoryListsNothingAndLimitsAreValidated()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = new RunReadStore(scope.Db, new FakeTimeProvider(Now));

            var list = await store.ListAsync(10, Ct);

            Assert.Empty(list.Items);
            Assert.Equal(0, list.Total);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                store.ListPostingsAsync(Guid.NewGuid(), 0, Ct)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunReadStore(null!, new FakeTimeProvider(Now))
            );
            Assert.Throws<ArgumentNullException>(() => new RunReadStore(scope.Db, null!));
        }

        [Fact]
        public async Task PostingsAreThoseSeenInsideTheRunWindow()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var run = ScrapeRun.Create(profile, Now);
            run.Start(Now);
            var notStarted = ScrapeRun.Create(profile, Now);
            var before = Posting("100", Now.AddHours(-2));
            var during = Posting("200", Now.AddMinutes(1));
            var refreshed = Posting("300", Now.AddDays(-3));
            refreshed.Refresh(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = "300",
                    Url = "https://www.naukri.com/job-listings-dev-300",
                    Title = "Developer 300",
                    Description = "Enriched.",
                },
                Now.AddMinutes(2)
            );
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(run, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(notStarted, Ct);
                var postings = scope.Get<IJobPostingRepository>();
                await postings.AddAsync(before, Ct);
                await postings.AddAsync(during, Ct);
                await postings.AddAsync(refreshed, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            // Still executing: the window runs to the clock's now.
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            var seen = await store.ListPostingsAsync(run.Id.Value, 50, Ct);
            Assert.Equal(["300", "200"], seen!.Items.Select(p => p.ExternalJobId));
            Assert.False(seen.Items[0].IsNew);
            Assert.True(seen.Items[0].Enriched);
            Assert.True(seen.Items[1].IsNew);
            Assert.Equal(["C#"], seen.Items[1].Skills);
            Assert.Equal("Unknown", seen.Items[1].WorkMode);

            Assert.Empty((await store.ListPostingsAsync(notStarted.Id.Value, 50, Ct))!.Items);
            Assert.Null(await store.ListPostingsAsync(Guid.NewGuid(), 50, Ct));
        }

        [Fact]
        public async Task ListExecutingSkipsPausedAndTerminalRuns()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var searching = ScrapeRun.Create(profile, Now);
            searching.Start(Now);
            searching.BeginSearching();
            var paused = ScrapeRun.Create(profile, Now);
            paused.Start(Now);
            paused.BeginSearching();
            paused.PauseForChallenge(Domain.Access.ChallengeKind.Captcha, Now);
            var failed = ScrapeRun.Create(profile, Now);
            failed.Start(Now);
            failed.Fail("boom", Now);
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                var runs = scope.Get<IScrapeRunRepository>();
                await runs.AddAsync(searching, Ct);
                await runs.AddAsync(paused, Ct);
                await runs.AddAsync(failed, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var executing = await reload.Get<IScrapeRunRepository>().ListExecutingAsync(Ct);
            Assert.Equal(searching.Id, Assert.Single(executing).Id);
        }
    }
}
