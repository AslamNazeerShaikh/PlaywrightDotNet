using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Logging;
using Xunit;
using static JobScrape.Application.Tests.Scraping.ScrapeHarness;

namespace JobScrape.Application.Tests.Scraping
{
    public sealed class RunScrapeFilteringTests
    {
        private static readonly DateTimeOffset Now = Start;

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static void UseCriteria(ScrapeHarness harness, SearchCriteria criteria)
        {
            harness.Profile.UpdateCriteria(criteria, Now);
        }

        private static SearchCriteria Criteria(
            int? postedWithinDays = null,
            string[]? requiredSkills = null
        )
        {
            return new SearchCriteria(
                "c#",
                ["Pune"],
                null,
                postedWithinDays,
                remoteOnly: false,
                requiredSkills ?? []
            );
        }

        private static async Task RunOkAsync(ScrapeHarness harness)
        {
            var result = await harness.Build().HandleAsync(harness.Request, Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        }

        private static JobDetailResult DatedDetail(string id, DateTimeOffset postedAt)
        {
            var snapshot = Detail(id).Detail!;
            return Detail(id) with { Detail = snapshot with { PostedAt = postedAt } };
        }

        [Fact]
        public async Task DateGateDropsStaleCardsButKeepsFreshAndUnknownDates()
        {
            var harness = new ScrapeHarness();
            UseCriteria(harness, Criteria(postedWithinDays: 1));
            harness.Source.Then(
                Page(
                    null,
                    Job("1") with { PostedAt = Now.AddHours(-2) },
                    Job("2") with { PostedAt = Now.AddDays(-2) },
                    Job("3"),
                    Job("4") with { PostedAt = Now.AddDays(-1) }
                )
            );

            await RunOkAsync(harness);

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(3, run.Stats.PostingsFound);
            Assert.Equal(["1", "3", "4"], harness.Store.Postings.Select(p => p.ExternalJobId.Value).Order());
            var drop = Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("stale posted date", StringComparison.Ordinal)
            );
            Assert.Equal(LogLevel.Warning, drop.Level);
        }

        [Fact]
        public async Task SkillMismatchDropsTheCardWhileSkillLessCardsStayCandidates()
        {
            var harness = new ScrapeHarness(requiredSkills: ["c#", "asp.net"]);
            harness.Source.Then(
                Page(
                    null,
                    Job("1") with { Skills = ["C#"] },
                    Job("2") with { Skills = ["Java"] },
                    Job("3")
                )
            );

            await RunOkAsync(harness);

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(2, run.Stats.PostingsFound);
            Assert.Equal(2, run.Stats.PostingsNew);
            Assert.Equal(["1", "3"], harness.Store.Postings.Select(p => p.ExternalJobId.Value).Order());
            var drop = Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("skill mismatch", StringComparison.Ordinal)
            );
            Assert.Contains("2", drop.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task MismatchedResightDropsBeforeRefreshWithoutBreakingCounts()
        {
            var harness = new ScrapeHarness(requiredSkills: ["c#"]);
            var seeded = JobPosting.Create(Job("1") with { Skills = ["C#"] }, Now);
            harness.Store.Postings.Add(seeded);
            harness.Source.Then(Page(null, Job("1") with { Skills = ["Java"] }));

            await RunOkAsync(harness);

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(0, run.Stats.PostingsFound);
            Assert.Equal(0, run.Stats.PostingsNew);
            Assert.Equal(0, run.Stats.PostingsUpdated);
            Assert.Equal(0, run.Stats.Failures);
            var stored = Assert.Single(harness.Store.Postings);
            Assert.Equal(["C#"], stored.Skills.Select(s => s.Name));
            Assert.Equal(Now, stored.LastSeenAt);
        }

        [Fact]
        public async Task PostedDateMismatchIsLoggedCountedAndDetailWins()
        {
            var harness = new ScrapeHarness(maxDetails: 1);
            harness.Source
                .Then(Page(null, Job("1") with { PostedAt = Now.AddHours(-1) }))
                .ThenDetails(DatedDetail("1", Now.AddHours(-5)));

            await RunOkAsync(harness);

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(1, run.Stats.PostedDateMismatches);
            Assert.Equal(Now.AddHours(-5), Assert.Single(harness.Store.Postings).PostedAt);
            var mismatch = Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("posted-date mismatch", StringComparison.Ordinal)
            );
            Assert.Equal(LogLevel.Warning, mismatch.Level);
        }

        [Fact]
        public async Task AgreeingPostedDatesAreNotCounted()
        {
            var harness = new ScrapeHarness(maxDetails: 1);
            harness.Source
                .Then(Page(null, Job("1") with { PostedAt = Now.AddHours(-1) }))
                .ThenDetails(DatedDetail("1", Now.AddHours(-1)));

            await RunOkAsync(harness);

            Assert.Equal(0, Assert.Single(harness.Store.Runs).Stats.PostedDateMismatches);
            Assert.DoesNotContain(
                harness.Logger.Entries,
                e => e.Message.Contains("posted-date mismatch", StringComparison.Ordinal)
            );
        }

        [Fact]
        public async Task ProgressIsSavedEachPageAlongsideResults()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("next", Job("1")), Page(null, Job("2")));

            await RunOkAsync(harness);

            var run = Assert.Single(harness.Store.Runs);
            var progress = Assert.Single(harness.Store.Progress);
            Assert.Equal(run.Id, progress.Id);
            Assert.Equal(2, progress.PagesCompleted);
            Assert.Null(progress.NextCursor);
            Assert.Equal(["1", "2"], progress.SeenExternalIds.Order());
            Assert.True(harness.Store.Saves >= 3);
        }

        [Fact]
        public async Task RepeatedCardsAreCountedOnceAndProgressKeepsSeenIds()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("next", Job("1")), Page(null, Job("2"), Job("1")));

            await RunOkAsync(harness);

            var progress = Assert.Single(harness.Store.Progress);
            Assert.Equal(2, progress.PagesCompleted);
            Assert.Equal(["1", "2"], progress.SeenExternalIds.Order());
            Assert.Equal(2, Assert.Single(harness.Store.Runs).Stats.PostingsFound);
        }
    }
}
