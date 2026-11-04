using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class RunStatsTests
    {
        [Fact]
        public void EmptyHasAllZeroCounts()
        {
            var stats = RunStats.Empty;

            Assert.Equal(0, stats.PagesFetched);
            Assert.Equal(0, stats.PostingsFound);
            Assert.Equal(0, stats.PostingsNew);
            Assert.Equal(0, stats.PostingsUpdated);
            Assert.Equal(0, stats.DetailsFetched);
            Assert.Equal(0, stats.Challenges);
            Assert.Equal(0, stats.Failures);
        }

        [Fact]
        public void AddMethodsGrowOnlyTheirCounterAndLeaveOriginalUnchanged()
        {
            var stats = RunStats
                .Empty.AddPage(new PageStats(5, 3, 1))
                .AddDetail()
                .AddChallenge()
                .AddFailure();

            Assert.Equal(1, stats.PagesFetched);
            Assert.Equal(5, stats.PostingsFound);
            Assert.Equal(3, stats.PostingsNew);
            Assert.Equal(1, stats.PostingsUpdated);
            Assert.Equal(1, stats.DetailsFetched);
            Assert.Equal(1, stats.Challenges);
            Assert.Equal(1, stats.Failures);
            Assert.Equal(0, stats.PostedDateMismatches);
            Assert.Equal(0, RunStats.Empty.PagesFetched);
        }

        [Fact]
        public void PostedDateMismatchGrowsOnlyItsCounter()
        {
            var stats = RunStats.Empty.AddPostedDateMismatch().AddPostedDateMismatch();

            Assert.Equal(2, stats.PostedDateMismatches);
            Assert.Equal(0, stats.Failures);
            Assert.Equal(0, RunStats.Empty.PostedDateMismatches);
        }

        [Fact]
        public void AddPageRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => RunStats.Empty.AddPage(null!));
        }
    }
}
