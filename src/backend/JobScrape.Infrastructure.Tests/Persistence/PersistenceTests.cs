using JobScrape.Application.Abstractions;
using JobScrape.Domain;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class PersistenceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfile Profile(string name = "backend", SourceKey? source = null)
        {
            return SearchProfile.Create(
                name,
                source ?? SourceKey.Naukri,
                new SearchCriteria(
                    "asp.net core",
                    ["Pune", "Hyderabad"],
                    6,
                    7,
                    remoteOnly: false,
                    ["C#", "ASP.NET Core"]
                ),
                new RunLimits(3, 60, 10, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
                Now
            );
        }

        private static JobSnapshot Snapshot(
            string id = "123",
            string title = "Senior .NET Developer"
        )
        {
            return new JobSnapshot
            {
                Source = SourceKey.Naukri,
                ExternalJobId = id,
                Url = $"https://www.naukri.com/job-listings-x-{id}?utm_source=feed",
                Title = title,
            };
        }

        private static JobSnapshot FullSnapshot()
        {
            return Snapshot() with
            {
                Company = "Contoso",
                CompanyUrl = "https://www.naukri.com/contoso-jobs",
                Location = new JobLocation(
                    "Pune, Hyderabad",
                    "Pune",
                    "MH",
                    "India",
                    WorkMode.Hybrid
                ),
                ExperienceRaw = "5-8 Yrs",
                ExperienceMinYears = 5,
                ExperienceMaxYears = 8,
                SalaryRaw = "12-18 Lacs P.A.",
                Salary = new SalaryRange(1_200_000m, 1_800_000m, "INR", SalaryPeriod.Year),
                PostedRaw = "3 days ago",
                PostedAt = Now.AddDays(-3),
                Description = "Build APIs.\nShip features.",
                Snippet = "Build APIs",
                Skills = ["C#", "ASP.NET Core"],
                ApplyUrl = "https://careers.contoso.example/apply/42",
                LowConfidence = true,
            };
        }

        // ---- schema ----

        [Fact]
        public async Task MigrationsApplyAndNothingIsPending()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();

            Assert.Empty(await scope.Db.Database.GetPendingMigrationsAsync(Ct));
            Assert.True((await scope.Db.Database.GetAppliedMigrationsAsync(Ct)).Count() >= 3);
        }

        [Fact]
        public async Task ConnectionsUseWalForeignKeysAndBusyTimeout()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            await scope.Db.Database.OpenConnectionAsync(Ct);
            var connection = scope.Db.Database.GetDbConnection();

            async Task<string?> PragmaAsync(string name)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA {name};";
                return (await command.ExecuteScalarAsync(Ct))?.ToString();
            }

            Assert.Equal("wal", await PragmaAsync("journal_mode"));
            Assert.Equal("1", await PragmaAsync("foreign_keys"));
            Assert.Equal("5000", await PragmaAsync("busy_timeout"));
        }

        [Fact]
        public async Task InitialMigrationIsReversible()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            await scope.Db.Database.OpenConnectionAsync(Ct);
            await using var count = scope.Db.Database.GetDbConnection().CreateCommand();
            count.CommandText =
                "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '\\_\\_%' ESCAPE '\\' AND name NOT LIKE 'sqlite%';";
            Assert.Equal(7L, await count.ExecuteScalarAsync(Ct));

            await scope
                .Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                .MigrateAsync("0", Ct);

            Assert.Equal(0L, await count.ExecuteScalarAsync(Ct));
        }

        // ---- round trips ----

        [Fact]
        public async Task SearchProfileRoundTrips()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile();
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var loaded = await reload.Get<ISearchProfileRepository>().GetAsync(profile.Id, Ct);

            Assert.NotNull(loaded);
            Assert.Equal(profile.Name, loaded.Name);
            Assert.Same(SourceKey.Naukri, loaded.Source);
            Assert.Equal(profile.Criteria, loaded.Criteria);
            Assert.Equal(profile.Limits, loaded.Limits);
            Assert.True(loaded.Enabled);
            Assert.Equal(Now, loaded.CreatedAt);
            Assert.Equal(Now, loaded.UpdatedAt);
            Assert.Empty(loaded.DomainEvents);
            Assert.Null(
                await reload.Get<ISearchProfileRepository>().GetAsync(SearchProfileId.New(), Ct)
            );
        }

        [Fact]
        public async Task ProfileNamesAreUnique()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var profiles = scope.Get<ISearchProfileRepository>();
            await profiles.AddAsync(Profile("same"), Ct);
            await profiles.AddAsync(Profile("same"), Ct);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.Get<IUnitOfWork>().SaveChangesAsync(Ct)
            );
        }

        [Fact]
        public async Task RunProgressRoundTripsAndOverwrites()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var id = ScrapeRunId.New();
            await using (var scope = database.Scope())
            {
                var stored = ScrapeRunProgress.Start(id, Now);
                stored.RecordPage(1, "next", ["101"], Now);
                await scope.Get<IScrapeRunProgressRepository>().SaveAsync(stored, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using (var scope = database.Scope())
            {
                var loaded = await scope.Get<IScrapeRunProgressRepository>().GetAsync(id, Ct);

                Assert.NotNull(loaded);
                Assert.Equal(1, loaded.PagesCompleted);
                Assert.Equal("next", loaded.NextCursor);
                Assert.Equal(["101"], loaded.SeenExternalIds);
                loaded.RecordPage(2, null, ["101", "102"], Now.AddMinutes(1));
                await scope.Get<IScrapeRunProgressRepository>().SaveAsync(loaded, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var reloaded = await reload.Get<IScrapeRunProgressRepository>().GetAsync(id, Ct);

            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded.PagesCompleted);
            Assert.Null(reloaded.NextCursor);
            Assert.Equal(["101", "102"], reloaded.SeenExternalIds);
            Assert.Null(
                await reload.Get<IScrapeRunProgressRepository>().GetAsync(ScrapeRunId.New(), Ct)
            );
        }

        [Fact]
        public async Task JobPostingRoundTripsEveryField()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var posting = JobPosting.Create(FullSnapshot(), Now);
            await using (var scope = database.Scope())
            {
                await scope.Get<IJobPostingRepository>().AddAsync(posting, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var loaded = await reload
                .Get<IJobPostingRepository>()
                .FindAsync(SourceKey.Naukri, ExternalJobId.Create("123"), Ct);

            Assert.NotNull(loaded);
            Assert.Equal(posting.Id, loaded.Id);
            Assert.Equal(posting.Url, loaded.Url);
            Assert.Equal(posting.Title, loaded.Title);
            Assert.Equal(posting.Company, loaded.Company);
            Assert.Equal(posting.CompanyUrl, loaded.CompanyUrl);
            Assert.Equal(posting.Location, loaded.Location);
            Assert.Equal(5, loaded.ExperienceMinYears);
            Assert.Equal(8, loaded.ExperienceMaxYears);
            Assert.Equal(posting.SalaryRaw, loaded.SalaryRaw);
            Assert.Equal(posting.Salary, loaded.Salary);
            Assert.Equal(posting.PostedRaw, loaded.PostedRaw);
            Assert.Equal(Now.AddDays(-3), loaded.PostedAt);
            Assert.Equal(posting.Description, loaded.Description);
            Assert.Equal(posting.Snippet, loaded.Snippet);
            Assert.Equal(posting.Skills, loaded.Skills);
            Assert.Equal(posting.ApplyUrl, loaded.ApplyUrl);
            Assert.Equal(posting.ContentHash, loaded.ContentHash);
            Assert.Equal(JobPostingStatus.Active, loaded.Status);
            Assert.Equal(DataQualityIssues.ParseLowConfidence, loaded.QualityIssues);
            Assert.Equal(Now, loaded.FirstSeenAt);
        }

        [Fact]
        public async Task SourceAndExternalIdAreUnique()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            scope.Db.JobPostings.Add(JobPosting.Create(Snapshot(), Now));
            scope.Db.JobPostings.Add(JobPosting.Create(Snapshot(), Now));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.Db.SaveChangesAsync(Ct)
            );

            Assert.Contains("UNIQUE", error.InnerException?.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RefreshUpdatesOneRowAndPreservesFirstSeen()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                await scope
                    .Get<IJobPostingRepository>()
                    .AddAsync(JobPosting.Create(Snapshot(), Now), Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using (var scope = database.Scope())
            {
                var posting = await scope
                    .Get<IJobPostingRepository>()
                    .FindAsync(SourceKey.Naukri, ExternalJobId.Create("123"), Ct);
                posting!.Refresh(
                    FullSnapshot() with
                    {
                        Title = "Lead .NET Developer",
                    },
                    Now.AddDays(1)
                );
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var only = Assert.Single(await reload.Db.JobPostings.ToListAsync(Ct));
            Assert.Equal("Lead .NET Developer", only.Title.Value);
            Assert.Equal(["C#", "ASP.NET Core"], only.Skills.Select(s => s.Name));
            Assert.Equal(Now, only.FirstSeenAt);
            Assert.Equal(Now.AddDays(1), only.LastSeenAt);
        }

        [Fact]
        public async Task FindSeesPostingsStagedButNotSaved()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var postings = scope.Get<IJobPostingRepository>();
            var staged = JobPosting.Create(Snapshot(), Now);
            await postings.AddAsync(staged, Ct);

            var found = await postings.FindAsync(SourceKey.Naukri, ExternalJobId.Create("123"), Ct);

            Assert.Same(staged, found);
            Assert.Null(
                await postings.FindAsync(SourceKey.Naukri, ExternalJobId.Create("999"), Ct)
            );
        }

        [Fact]
        public async Task PausedRunRoundTripsAndResumesWhereItStopped()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = Profile();
            var run = ScrapeRun.Create(profile, Now);
            run.Start(Now);
            run.BeginSearching();
            run.RecordPage(new PageStats(20, 15, 3), Now);
            run.BeginEnriching();
            run.RecordDetail();
            run.PauseForChallenge(ChallengeKind.Captcha, Now.AddMinutes(1));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISearchProfileRepository>().AddAsync(profile, Ct);
                await scope.Get<IScrapeRunRepository>().AddAsync(run, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var loaded = await reload.Get<IScrapeRunRepository>().GetAsync(run.Id, Ct);

            Assert.NotNull(loaded);
            Assert.Equal(RunStage.PausedForChallenge, loaded.Stage);
            Assert.Equal(ChallengeKind.Captcha, loaded.PauseReason);
            Assert.Equal(run.Stats, loaded.Stats);
            Assert.Equal(run.Limits, loaded.Limits);
            Assert.Equal(profile.Id, loaded.ProfileId);
            Assert.Equal(Now, loaded.StartedAt);
            loaded.Resume(Now.AddHours(1));
            Assert.Equal(RunStage.Enriching, loaded.Stage);
            Assert.Null(await reload.Get<IScrapeRunRepository>().GetAsync(ScrapeRunId.New(), Ct));
        }

        [Fact]
        public async Task RunMustReferenceAnExistingProfile()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            await scope.Get<IScrapeRunRepository>().AddAsync(ScrapeRun.Create(Profile(), Now), Ct);

            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.Get<IUnitOfWork>().SaveChangesAsync(Ct)
            );

            Assert.Contains("FOREIGN KEY", error.InnerException?.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BrowserProfileRoundTripsCooldownState()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var profile = BrowserProfile.Create(
                "naukri-main",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Chrome
            );
            profile.MarkVerified(Now);
            profile.MarkChallenged(ChallengeKind.Captcha, Now, TimeSpan.FromMinutes(45));
            await using (var scope = database.Scope())
            {
                scope.Db.BrowserProfiles.Add(profile);
                await scope.Db.SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var loaded = await reload.Get<IBrowserProfileRepository>().GetAsync(profile.Id, Ct);

            Assert.NotNull(loaded);
            Assert.Equal("naukri/naukri-main", loaded.UserDataDirectory);
            Assert.Equal(BrowserChannel.Chrome, loaded.Channel);
            Assert.Equal(SessionState.CoolingDown, loaded.State);
            Assert.Equal(Now.AddMinutes(45), loaded.CooldownUntil);
            Assert.Equal(1, loaded.ConsecutiveChallenges);
            Assert.Equal(Now, loaded.LastVerifiedAt);
            Assert.False(loaded.CanLaunch(Now.AddMinutes(44)));
        }

        [Fact]
        public async Task QuotaIsFoundByUtcDayAndKeepsCounts()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);
            quota.TryStartRun(2, Now);
            quota.TryConsumePage(25, Now);
            quota.RecordChallenge(Now);
            await using (var scope = database.Scope())
            {
                await scope.Get<ISourceQuotaRepository>().AddAsync(quota, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var quotas = reload.Get<ISourceQuotaRepository>();
            var loaded = await quotas.FindAsync(SourceKey.Naukri, new DateOnly(2026, 11, 2), Ct);

            Assert.NotNull(loaded);
            Assert.Equal(1, loaded.RunsStarted);
            Assert.Equal(1, loaded.PagesFetched);
            Assert.Equal(1, loaded.Challenges);
            Assert.Null(await quotas.FindAsync(SourceKey.Naukri, new DateOnly(2026, 11, 3), Ct));
            Assert.Null(await quotas.FindAsync(SourceKey.Indeed, new DateOnly(2026, 11, 2), Ct));
        }

        [Fact]
        public async Task CircuitBreakerRoundTripsRecentFailures()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);
            breaker.RecordFailure(ChallengeKind.Captcha, Now);
            breaker.RecordSuccess(Now.AddHours(1));
            breaker.RecordFailure(ChallengeKind.Captcha, Now.AddHours(2));
            await using (var scope = database.Scope())
            {
                await scope.Get<ISourceCircuitBreakerRepository>().AddAsync(breaker, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using (var scope = database.Scope())
            {
                var loaded = await scope
                    .Get<ISourceCircuitBreakerRepository>()
                    .FindAsync(SourceKey.Naukri, Ct);
                Assert.NotNull(loaded);
                Assert.Equal([Now, Now.AddHours(2)], loaded.RecentFailures);

                // A third wall inside the window must open the reloaded breaker.
                loaded.RecordFailure(ChallengeKind.Captcha, Now.AddHours(3));
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var reopened = await reload
                .Get<ISourceCircuitBreakerRepository>()
                .FindAsync(SourceKey.Naukri, Ct);
            Assert.Equal(CircuitState.Open, reopened!.State);
            Assert.Equal(
                Now.AddHours(3) + SourceCircuitBreaker.ChallengeCooldown,
                reopened.OpenUntil
            );
            Assert.Null(
                await reload.Get<ISourceCircuitBreakerRepository>().FindAsync(SourceKey.Indeed, Ct)
            );
        }

        [Fact]
        public async Task TimestampsAreStoredAsComparableUtcTicks()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var india = new DateTimeOffset(2026, 11, 2, 13, 30, 0, TimeSpan.FromHours(5.5));
            await using (var scope = database.Scope())
            {
                scope.Db.JobPostings.Add(JobPosting.Create(Snapshot("1"), india));
                scope.Db.JobPostings.Add(JobPosting.Create(Snapshot("2"), Now.AddHours(1)));
                await scope.Db.SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var cutoff = Now.AddMinutes(30);
            var recent = await reload
                .Db.JobPostings.Where(p => p.LastSeenAt >= cutoff)
                .ToListAsync(Ct);

            Assert.Equal("2", Assert.Single(recent).ExternalJobId.Value);
            var first = await reload.Db.JobPostings.SingleAsync(p => p.LastSeenAt < cutoff, Ct);
            Assert.Equal(india, first.FirstSeenAt);
            Assert.Equal(TimeSpan.Zero, first.FirstSeenAt.Offset);
        }

        [Fact]
        public async Task CorruptStoredValueFailsLoudlyOnLoad()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                scope.Db.JobPostings.Add(JobPosting.Create(Snapshot(), Now));
                await scope.Db.SaveChangesAsync(Ct);
            }

            await using (var connection = new SqliteConnection(database.ConnectionString))
            {
                await connection.OpenAsync(Ct);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "UPDATE job_postings SET canonical_url = 'https://evil.example/x';";
                await command.ExecuteNonQueryAsync(Ct);
            }

            await using var reload = database.Scope();
            await Assert.ThrowsAsync<DomainException>(() => reload.Db.JobPostings.ToListAsync(Ct));
        }

        [Fact]
        public async Task NullJsonValueFailsLoudlyOnLoad()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                scope.Db.JobPostings.Add(JobPosting.Create(Snapshot(), Now));
                await scope.Db.SaveChangesAsync(Ct);
            }

            await using (var connection = new SqliteConnection(database.ConnectionString))
            {
                await connection.OpenAsync(Ct);
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE job_postings SET skills = 'null';";
                await command.ExecuteNonQueryAsync(Ct);
            }

            await using var reload = database.Scope();
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                reload.Db.JobPostings.ToListAsync(Ct)
            );
        }
    }
}
