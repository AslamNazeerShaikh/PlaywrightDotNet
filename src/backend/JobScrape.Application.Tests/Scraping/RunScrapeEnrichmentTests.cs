using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;
using static JobScrape.Application.Tests.Scraping.ScrapeHarness;

namespace JobScrape.Application.Tests.Scraping
{
    public sealed class RunScrapeEnrichmentTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<ScrapeRunSummary> RunAsync(
            ScrapeHarness harness,
            bool enrich = true
        )
        {
            var result = await harness
                .Build()
                .HandleAsync(harness.Request with { Enrich = enrich }, Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return result.Value;
        }

        private static JobPosting Posting(ScrapeHarness harness, string id)
        {
            return harness.Store.Postings.Single(p => p.ExternalJobId.Value == id);
        }

        [Fact]
        public async Task EnrichesNewPostingsWithinTheCapThenCompletes()
        {
            var harness = new ScrapeHarness(maxDetails: 2);
            harness
                .Source.Then(Page(null, Job("1"), Job("2"), Job("3")))
                .ThenDetails(Detail("1"), Detail("2"));

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(2, summary.Stats.DetailsFetched);
            Assert.Equal(
                ["1", "2"],
                harness.Source.DetailRequests.Select(j => j.ExternalJobId.Value)
            );
            Assert.Equal("Build APIs.", Posting(harness, "1").Description);
            Assert.Equal("C#", Assert.Single(Posting(harness, "2").Skills).Name);
            Assert.Null(Posting(harness, "3").Description);
            Assert.Equal(2, Assert.Single(harness.Store.Quotas).DetailsFetched);
            Assert.Equal(2, harness.Delay.Waits.Count);
        }

        [Fact]
        public async Task AlreadyEnrichedPostingsAreNotVisitedAgain()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness
                .Source.Then(Page(null, Job("1")), Page(null, Job("1")))
                .ThenDetails(Detail("1"));

            await RunAsync(harness);
            harness.Clock.Advance(TimeSpan.FromHours(1));
            var second = await RunAsync(harness);

            Assert.Equal(0, second.Stats.DetailsFetched);
            Assert.Single(harness.Source.DetailRequests);
            Assert.Equal("Build APIs.", Posting(harness, "1").Description);
        }

        [Fact]
        public async Task NoEnrichSkipsDetails()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness.Source.Then(Page(null, Job("1")));

            var summary = await RunAsync(harness, enrich: false);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Empty(harness.Source.DetailRequests);
        }

        [Fact]
        public async Task AdapterWithoutDetailSupportIsNotAskedForDetails()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness.Source.SupportsDetails = false;
            harness.Source.Then(Page(null, Job("1")));

            await RunAsync(harness);

            Assert.Empty(harness.Source.DetailRequests);
        }

        [Fact]
        public async Task NoPagesMeansNoEnrichment()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness.Settings = harness.Settings with { DailyPageCap = 0 };

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Empty(harness.Source.DetailRequests);
        }

        [Fact]
        public async Task DetailQuotaExhaustionStopsEnrichmentAndCompletes()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness.Settings = harness.Settings with { DailyDetailCap = 1 };
            harness.Source.Then(Page(null, Job("1"), Job("2"))).ThenDetails(Detail("1"));

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(1, summary.Stats.DetailsFetched);
        }

        [Fact]
        public async Task UnusableDetailsAreSkippedAsFailures()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness
                .Source.Then(Page(null, Job("1"), Job("2"), Job("3"), Job("4")))
                .ThenDetails(
                    new JobDetailResult(null, ChallengeAssessment.Clear),
                    Detail("999"),
                    DetailWall(ChallengeKind.TransientError),
                    DetailWall(ChallengeKind.TransientError),
                    Detail("4")
                );

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(4, summary.Stats.DetailsFetched);
            Assert.Equal(3, summary.Stats.Failures);
            Assert.Null(Posting(harness, "2").Description);
            Assert.Equal("Build APIs.", Posting(harness, "4").Description);
            Assert.Equal(5, harness.Source.DetailRequests.Count);
        }

        [Fact]
        public async Task CaptchaDuringEnrichmentPausesInEnrichingAndResumesThere()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness
                .Source.Then(Page(null, Job("1"), Job("2")))
                .ThenDetails(Detail("1"), DetailWall(ChallengeKind.Captcha));

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.PausedForChallenge, summary.Stage);
            Assert.Equal("Captcha", summary.PauseReason);
            Assert.NotNull(summary.ArtifactDirectory);
            Assert.Equal(SessionState.CoolingDown, harness.Browser.State);

            var run = Assert.Single(harness.Store.Runs);
            run.Resume(ScrapeHarness.Start.AddHours(2));
            Assert.Equal(RunStage.Enriching, run.Stage);
        }

        [Fact]
        public async Task RateLimitDuringEnrichmentStopsEnrichingAndCompletes()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness
                .Source.Then(Page(null, Job("1"), Job("2")))
                .ThenDetails(Detail("1"), DetailWall(ChallengeKind.RateLimited));

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(1, summary.Stats.DetailsFetched);
            Assert.Equal(1, Assert.Single(harness.Store.Quotas).Challenges);
            Assert.Equal(1, Assert.Single(harness.Store.Circuits).ConsecutiveFailures);
            Assert.Equal(2, harness.Source.DetailRequests.Count);
        }

        [Fact]
        public async Task ChangedDetailPageFailsTheRunWithEvidence()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness
                .Source.Then(Page(null, Job("1")))
                .ThenDetails(DetailWall(ChallengeKind.SourceChanged));

            var summary = await RunAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.NotNull(summary.ArtifactDirectory);
            Assert.Single(harness.Store.Postings);
        }

        [Fact]
        public async Task DetailLogsUseTheStepName()
        {
            var harness = new ScrapeHarness(maxDetails: 5);
            harness.Settings = harness.Settings with { DailyDetailCap = 1 };
            harness
                .Source.Then(Page(null, Job("1"), Job("2")))
                .ThenDetails(DetailWall(ChallengeKind.TransientError), Detail("1"));

            await RunAsync(harness);

            var retry = Assert.Single(
                harness.Logger.Entries,
                e => e.Properties.ContainsKey("Step")
            );
            Assert.Equal("detail 1", retry.Properties["Step"]);
            var stop = Assert.Single(
                harness.Logger.Entries,
                e => e.Properties.ContainsKey("Detail")
            );
            Assert.Equal(2, stop.Properties["Detail"]);
        }
    }
}
