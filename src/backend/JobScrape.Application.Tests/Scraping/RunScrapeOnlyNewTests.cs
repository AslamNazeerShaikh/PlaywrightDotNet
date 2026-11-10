using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Sourcing;
using Xunit;
using static JobScrape.Application.Tests.Scraping.ScrapeHarness;

namespace JobScrape.Application.Tests.Scraping
{
    public sealed class RunScrapeOnlyNewTests
    {
        private static readonly DateTimeOffset Past = Start.AddDays(-7);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static void SeedKnown(ScrapeHarness harness, params string[] ids)
        {
            foreach (var id in ids)
            {
                harness.Store.Postings.Add(JobPosting.Create(Job(id), Past));
            }
        }

        private static JobPosting Posting(ScrapeHarness harness, string id)
        {
            return harness.Store.Postings.Single(p => p.ExternalJobId.Value == id);
        }

        [Fact]
        public async Task KnownRowsRefreshWithoutDuplicating()
        {
            var harness = new ScrapeHarness { OnlyNew = true };
            SeedKnown(harness, "1");
            harness.Source.Then(Page(null, Job("1"), Job("2")));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(1, run.Stats.PostingsNew);
            Assert.Equal(2, run.Stats.PostingsFound);
            Assert.Equal(2, harness.Store.Postings.Count);
            var known = Posting(harness, "1");
            Assert.Equal(Start, known.LastSeenAt);
            Assert.Equal(Past, known.FirstSeenAt);
        }

        [Fact]
        public async Task KnownRowsDoNotConsumeTheJobCap()
        {
            var harness = new ScrapeHarness(maxJobs: 2) { OnlyNew = true };
            SeedKnown(harness, "1");
            harness.Source.Then(Page("next", Job("1"), Job("2")), Page(null, Job("3"), Job("4")));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(RunStage.Completed, result.Value.Stage);
            Assert.Equal(2, result.Value.Stats.PostingsNew);
            Assert.Equal(2, harness.Source.Requests.Count);
        }

        [Fact]
        public async Task KnownRowsOnTheSamePageDoNotConsumeThePageBudget()
        {
            var harness = new ScrapeHarness(maxJobs: 2) { OnlyNew = true };
            SeedKnown(harness, "1");
            harness.Source.Then(Page(null, Job("1"), Job("2"), Job("3")));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(2, result.Value.Stats.PostingsNew);
            Assert.Equal(2, harness.Store.Postings.Count(p => p.FirstSeenAt == Start));
        }

        [Fact]
        public async Task CompletesWithZeroNewAndRefreshesKnownRows()
        {
            var harness = new ScrapeHarness { OnlyNew = true };
            SeedKnown(harness, "1", "2");
            harness.Source.Then(Page(null, Job("1"), Job("2")));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(RunStage.Completed, result.Value.Stage);
            Assert.Equal(0, result.Value.Stats.PostingsNew);
            Assert.Equal(Start, Posting(harness, "1").LastSeenAt);
            Assert.Equal(Start, Posting(harness, "2").LastSeenAt);
        }

        [Fact]
        public async Task EnrichesFreshBeforeKnown()
        {
            var harness = new ScrapeHarness(maxDetails: 1) { OnlyNew = true };
            SeedKnown(harness, "1");
            harness.Source.Then(Page(null, Job("1"), Job("2"))).ThenDetails(Detail("2"));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(["2"], harness.Source.DetailRequests.Select(j => j.ExternalJobId.Value));
            Assert.Equal("Build APIs.", Posting(harness, "2").Description);
            Assert.Null(Posting(harness, "1").Description);
        }

        [Fact]
        public async Task SummaryCarriesOnlyNew()
        {
            var onlyNew = new ScrapeHarness { OnlyNew = true };
            onlyNew.Source.Then(Page(null, Job("1")));

            var onlyNewResult = await onlyNew.Build().HandleAsync(onlyNew.Request, Ct);
            Assert.True(
                onlyNewResult.IsSuccess,
                onlyNewResult.IsFailure ? onlyNewResult.Error.Code : null
            );
            Assert.True(onlyNewResult.Value.OnlyNew);

            var full = new ScrapeHarness();
            full.Source.Then(Page(null, Job("1")));

            var fullResult = await full.Build().HandleAsync(full.Request, Ct);
            Assert.True(fullResult.IsSuccess, fullResult.IsFailure ? fullResult.Error.Code : null);
            Assert.False(fullResult.Value.OnlyNew);
        }
    }
}
