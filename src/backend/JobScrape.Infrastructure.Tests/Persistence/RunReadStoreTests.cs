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
                new SearchCriteria("c#", ["Pune"], null, null, false, []),
                new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                Now
            );
        }

        private static JobPosting Posting(
            string id,
            DateTimeOffset seen,
            string? location = null,
            string? company = null
        )
        {
            return JobPosting.Create(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = id,
                    Url = $"https://www.naukri.com/job-listings-dev-{id}",
                    Title = $"Developer {id}",
                    Skills = ["C#"],
                    Company = company,
                    Location = location is null
                        ? null
                        : new JobLocation(location, null, null, null, WorkMode.Unknown),
                },
                seen
            );
        }

        private static async Task<Guid> SeedPostingsRunAsync(
            TestDatabase database,
            (string Id, string? Location, string? Company)[] specs
        )
        {
            var profile = Profile("backend");
            var run = ScrapeRun.Create(profile, Now);
            run.Start(Now);
            run.ResumeProgress(Now)
                .RecordPage(1, null, specs.Select(spec => spec.Id).ToArray(), Now.AddMinutes(2));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(run, Ct);
                var postings = scope.Get<IJobPostingRepository>();
                foreach (var spec in specs)
                {
                    await postings.AddAsync(
                        Posting(spec.Id, Now.AddMinutes(1), spec.Location, spec.Company),
                        Ct
                    );
                }
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }
            return run.Id.Value;
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

            var list = await store.ListAsync(0, 10, Ct);
            Assert.Equal(2, list.Total);
            Assert.Equal([newer.Id.Value, older.Id.Value], list.Items.Select(r => r.Id));
            Assert.All(list.Items, r => Assert.Equal("backend", r.ProfileName));
            Assert.Equal("StartingSession", list.Items[0].Stage);
            Assert.False(list.Items[0].IsTerminal);
            Assert.Equal(3, list.Items[0].MaxPages);

            Assert.Single((await store.ListAsync(0, 1, Ct)).Items);
            Assert.Single((await store.ListAsync(1, 1, Ct)).Items);
            Assert.Equal(older.Id.Value, (await store.ListAsync(1, 1, Ct)).Items[0].Id);
            Assert.Equal(
                [older.Id.Value],
                (await store.ListAsync(1, 2, Ct)).Items.Select(r => r.Id)
            );
            Assert.Empty((await store.ListAsync(5, 10, Ct)).Items);
            Assert.Equal("Pending", (await store.GetAsync(older.Id.Value, Ct))!.Stage);
            Assert.Null(await store.GetAsync(Guid.NewGuid(), Ct));
            Assert.Null(await store.GetAsync(Guid.Empty, Ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListAsync(0, 0, Ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                store.ListAsync(-1, 10, Ct)
            );
            Assert.Empty(reload.Db.ChangeTracker.Entries());
        }

        [Fact]
        public async Task AnEmptyHistoryListsNothingAndLimitsAreValidated()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = new RunReadStore(scope.Db, new FakeTimeProvider(Now));

            var list = await store.ListAsync(0, 10, Ct);

            Assert.Empty(list.Items);
            Assert.Equal(0, list.Total);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                store.ListPostingsAsync(Guid.NewGuid(), 0, 0, Ct)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunReadStore(null!, new FakeTimeProvider(Now))
            );
            Assert.Throws<ArgumentNullException>(() => new RunReadStore(scope.Db, null!));
        }

        [Fact]
        public async Task PostingsAreThoseInTheRunSeenSet()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var run = ScrapeRun.Create(profile, Now);
            run.Start(Now);
            run.ResumeProgress(Now).RecordPage(1, null, ["200", "300"], Now.AddMinutes(2));
            var notStarted = ScrapeRun.Create(profile, Now);
            var before = Posting("100", Now.AddHours(-2));
            var during = Posting("200", Now.AddMinutes(1));
            var unrecorded = Posting("400", Now.AddMinutes(1));
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
                await postings.AddAsync(unrecorded, Ct);
                await postings.AddAsync(refreshed, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            // Still executing: the run lists what its progress recorded so far.
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            var seen = await store.ListPostingsAsync(run.Id.Value, 0, 50, Ct);
            Assert.Equal(["300", "200"], seen!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, seen.Total);
            Assert.DoesNotContain(seen.Items, p => p.ExternalJobId == "400");
            Assert.False(seen.Items[0].IsNew);
            Assert.True(seen.Items[0].Enriched);
            Assert.True(seen.Items[1].IsNew);
            Assert.Equal(["C#"], seen.Items[1].Skills);
            Assert.Equal("Unknown", seen.Items[1].WorkMode);

            var second = await store.ListPostingsAsync(run.Id.Value, 1, 1, Ct);
            Assert.Equal(["200"], second!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, second.Total);

            Assert.Empty((await store.ListPostingsAsync(notStarted.Id.Value, 0, 50, Ct))!.Items);
            Assert.Equal(0, (await store.ListPostingsAsync(notStarted.Id.Value, 0, 50, Ct))!.Total);
            Assert.Null(await store.ListPostingsAsync(Guid.NewGuid(), 0, 50, Ct));
        }

        [Fact]
        public async Task LocationsFilterMatchesAnyCommaSeparatedPartCaseInsensitively()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var runId = await SeedPostingsRunAsync(
                database,
                [
                    ("p1", "Pune, Mumbai", "Contoso"),
                    ("p2", "Delhi", "Contoso"),
                    ("p3", "Pune", "Fabrikam"),
                    ("p4", null, null),
                ]
            );

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            // "Pune, Mumbai" matches a Pune filter: the raw text is split on commas.
            var pune = await store.ListPostingsAsync(runId, 0, 50, Ct, locations: ["Pune"]);
            Assert.Equal(["p1", "p3"], pune!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, pune.Total);

            // Case does not matter.
            var lower = await store.ListPostingsAsync(runId, 0, 50, Ct, locations: ["pune"]);
            Assert.Equal(["p1", "p3"], lower!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, lower.Total);

            // Within one axis the values combine with OR.
            var both = await store.ListPostingsAsync(
                runId,
                0,
                50,
                Ct,
                locations: ["Pune", "Delhi"]
            );
            Assert.Equal(["p1", "p2", "p3"], both!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(3, both.Total);

            // A posting without a location never matches a location filter.
            var nowhere = await store.ListPostingsAsync(runId, 0, 50, Ct, locations: ["Nowhere"]);
            Assert.Empty(nowhere!.Items);
            Assert.Equal(0, nowhere.Total);
        }

        [Fact]
        public async Task CompaniesFilterIsAnExactCaseInsensitiveMatch()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var runId = await SeedPostingsRunAsync(
                database,
                [
                    ("p1", "Pune, Mumbai", "Contoso"),
                    ("p2", "Delhi", "Contoso"),
                    ("p3", "Pune", "Fabrikam"),
                    ("p4", null, null),
                ]
            );

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            var exact = await store.ListPostingsAsync(runId, 0, 50, Ct, companies: ["contoso"]);
            Assert.Equal(["p1", "p2"], exact!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, exact.Total);

            // No substring matching: "Cont" is not a company here.
            var partial = await store.ListPostingsAsync(runId, 0, 50, Ct, companies: ["Cont"]);
            Assert.Empty(partial!.Items);
            Assert.Equal(0, partial.Total);

            // A posting without a company never matches a company filter.
            var fab = await store.ListPostingsAsync(runId, 0, 50, Ct, companies: ["Fabrikam"]);
            Assert.Equal(["p3"], fab!.Items.Select(p => p.ExternalJobId));
        }

        [Fact]
        public async Task BothAxesCombineWithAndAndPagingAppliesPostFilter()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var runId = await SeedPostingsRunAsync(
                database,
                [
                    ("p1", "Pune, Mumbai", "Contoso"),
                    ("p2", "Delhi", "Contoso"),
                    ("p3", "Pune", "Fabrikam"),
                    ("p4", null, null),
                ]
            );

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            var anded = await store.ListPostingsAsync(
                runId,
                0,
                50,
                Ct,
                locations: ["Pune"],
                companies: ["Fabrikam"]
            );
            Assert.Equal(["p3"], anded!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(1, anded.Total);

            // No posting is both in Mumbai and at Fabrikam.
            var empty = await store.ListPostingsAsync(
                runId,
                0,
                50,
                Ct,
                locations: ["Mumbai"],
                companies: ["Fabrikam"]
            );
            Assert.Empty(empty!.Items);
            Assert.Equal(0, empty.Total);

            // The total counts the filtered set; paging slices it.
            var page = await store.ListPostingsAsync(runId, 1, 1, Ct, locations: ["Pune"]);
            Assert.Equal(["p3"], page!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(2, page.Total);
        }

        [Fact]
        public async Task BlankFiltersAreUnfilteredAndUnknownRunsStayNull()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var runId = await SeedPostingsRunAsync(
                database,
                [("p1", "Pune, Mumbai", "Contoso"), ("p2", "Delhi", "Contoso")]
            );

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            var blank = await store.ListPostingsAsync(
                runId,
                0,
                50,
                Ct,
                locations: ["  ", ""],
                companies: []
            );
            Assert.Equal(2, blank!.Total);
            Assert.Equal(["p1", "p2"], blank.Items.Select(p => p.ExternalJobId));

            Assert.Null(
                await store.ListPostingsAsync(
                    Guid.NewGuid(),
                    0,
                    50,
                    Ct,
                    locations: ["Pune"],
                    companies: ["Contoso"]
                )
            );
        }

        [Fact]
        public async Task FacetsAreDistinctSortedAndDeduped()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var runId = await SeedPostingsRunAsync(
                database,
                [
                    ("q1", "Pune, Mumbai", "Contoso"),
                    ("q2", "pune", "contoso"),
                    ("q3", null, "Fabrikam"),
                    ("q4", "Delhi", null),
                ]
            );
            var profile = Profile("empty");
            var notStarted = ScrapeRun.Create(profile, Now);
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(notStarted, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var store = new RunReadStore(reload.Db, new FakeTimeProvider(Now.AddMinutes(3)));

            Assert.Equal(
                ["Delhi", "Mumbai", "Pune"],
                await store.ListPostingLocationsAsync(runId, Ct)
            );
            Assert.Equal(["Contoso", "Fabrikam"], await store.ListPostingCompaniesAsync(runId, Ct));
            Assert.Empty((await store.ListPostingLocationsAsync(notStarted.Id.Value, Ct))!);
            Assert.Empty((await store.ListPostingCompaniesAsync(notStarted.Id.Value, Ct))!);
            Assert.Null(await store.ListPostingLocationsAsync(Guid.NewGuid(), Ct));
            Assert.Null(await store.ListPostingCompaniesAsync(Guid.NewGuid(), Ct));
        }

        [Fact]
        public async Task EarlierRunKeepsItsListAfterLaterRunRefreshesSharedPostings()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile("backend");
            var run1 = ScrapeRun.Create(profile, Now);
            run1.Start(Now);
            run1.BeginSearching();
            run1.RecordPage(new PageStats(2, 2, 0), Now.AddMinutes(2));
            run1.ResumeProgress(Now).RecordPage(1, null, ["A", "B"], Now.AddMinutes(2));
            run1.BeginPersisting();
            run1.Complete(Now.AddMinutes(3));
            var run2 = ScrapeRun.Create(profile, Now.AddHours(1));
            run2.Start(Now.AddHours(1));
            run2.ResumeProgress(Now.AddHours(1))
                .RecordPage(1, null, ["A", "B"], Now.AddHours(1).AddMinutes(2));
            var postingA = Posting("A", Now.AddMinutes(1));
            var postingB = Posting("B", Now.AddMinutes(2));
            // Later run re-sights both rows, moving LastSeenAt out of run1's window.
            postingA.Refresh(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = "A",
                    Url = "https://www.naukri.com/job-listings-dev-A",
                    Title = "Developer A",
                },
                Now.AddHours(1).AddMinutes(2)
            );
            postingB.Refresh(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = "B",
                    Url = "https://www.naukri.com/job-listings-dev-B",
                    Title = "Developer B",
                },
                Now.AddHours(1).AddMinutes(2)
            );
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                var runs = scope.Get<IScrapeRunRepository>();
                await runs.AddAsync(run1, Ct);
                await runs.AddAsync(run2, Ct);
                var postings = scope.Get<IJobPostingRepository>();
                await postings.AddAsync(postingA, Ct);
                await postings.AddAsync(postingB, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var store = new RunReadStore(
                reload.Db,
                new FakeTimeProvider(Now.AddHours(1).AddMinutes(5))
            );

            var seen = await store.ListPostingsAsync(run1.Id.Value, 0, 50, Ct);
            Assert.Equal(2, seen!.Total);
            Assert.Equal(["A", "B"], seen.Items.Select(p => p.ExternalJobId).Order());
            var export = await store.ListExportPostingsAsync(run1.Id.Value, 50, Ct);
            Assert.Equal(2, export!.Count);
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
